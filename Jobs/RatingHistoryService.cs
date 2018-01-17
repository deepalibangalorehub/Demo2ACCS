using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniversalTennis.Algorithm.Models;
using UniversalTennis.Algorithm.Service;

namespace UniversalTennis.Algorithm.Jobs
{
    public class RatingHistoryService : IRatingHistoryService
    {
        private readonly Config _config;
        private readonly ILogger _logger;

        public RatingHistoryService(IOptions<Config> config, ILoggerFactory logger)
        {
            _config = config.Value;
            _logger = logger.CreateLogger("UniversalTennis.Algorithm.RatingHistory");
        }

        [AutomaticRetry(Attempts = 0)]
        public void SaveDailyRatings(string algorithm)
        {
            IDbConnection connection = new SqlConnection(_config.ConnectionStrings.DefaultConnection);
            connection.Open();
            var transaction = connection.BeginTransaction();

            try
            {
                const string query =
                    @"SELECT Id, FinalRating, RatingReliability FROM PlayerRating";
                var playerRatings = connection.Query<PlayerRating>(query, transaction: transaction, commandTimeout: 1200);

                // select the ones from today so we overwrite if necessary
                var today = $"{DateTime.Now:MM-dd-yy}";

                var dailys = connection.Query<DailyRating>("SELECT * FROM DailyRating WHERE Date = @Today",
                    new { Today = today }, transaction, commandTimeout: 1200).ToList();

                foreach (var pr in playerRatings)
                {
                    var daily = dailys.FirstOrDefault(d => d.PlayerRatingId == pr.Id && d.Algorithm.Equals(algorithm));
                    // update existing
                    if (daily != null)
                    {
                        connection.Execute(
                            "UPDATE DailyRating SET Rating = @Rating, Reliability = @Reliability, Algorithm = @Algorithm, Date = @Date WHERE Id = @DailyId",
                            new { Rating = pr.FinalRating ?? 0, Reliability = pr.RatingReliability ?? 0, Algorithm = algorithm, Date = today, DailyId = daily.Id },
                            transaction, commandTimeout: 120);
                    }
                    // create new
                    else
                    {
                        connection.Execute(
                            "INSERT INTO DailyRating (Rating, Reliability, Algorithm, Date, PlayerRatingId) VALUES (@Rating, @Reliability, @Algorithm, @Date, @PlayerRatingId)",
                            new { Rating = pr.FinalRating ?? 0, Reliability = pr.RatingReliability ?? 0, Algorithm = algorithm, Date = today, PlayerRatingId = pr.Id },
                            transaction, commandTimeout: 120);
                    }
                }
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Failed to save daily ratings");
                EmailService.SendEmailNotification("chas@universaltennis.com", "Daily Ratings Error Report", "Daily Ratings Capture Failed");
                // Just so Hangfire marks as failed and shows the error
                throw new InvalidOperationException("Job failed to complete: " + ex.Message);
            }
            finally
            {
                transaction.Dispose();
                connection.Close();
            }
        }

        [AutomaticRetry(Attempts = 0)]
        public void SaveWeeklyAverage(string type, string algorithm)
        {
            IDbConnection connection = new SqlConnection(_config.ConnectionStrings.DefaultConnection);
            connection.Open();
            var transaction = connection.BeginTransaction();

            // prototype of anonymous type
            var updateList = Enumerable.Empty<object>()
                .Select(r => new { PlayerRatingId = 0, Rating = (double)0, RatingStatus = (RatingStatus)0, Type = (string)null, Date = DateTime.Now }) 
                .ToList();

            try
            {
                // get daily ratings from the past week
                var dailyDict = connection.Query<DailyRating>(
                    "SELECT PlayerRatingId, Rating, Reliability, Algorithm FROM DailyRating WHERE Date >= DATEADD(day, -7, GETDATE())",
                     transaction: transaction, commandTimeout: 1200).GroupBy(d => d.PlayerRatingId).ToDictionary(d => d.Key, d => d.ToList());
                var now = DateTime.Now;
                foreach (var playerDailys in dailyDict)
                {
                    var dailys = playerDailys.Value.Where(d => d.Algorithm.Equals(algorithm)).ToList();

                    // don't insert anything if the player has no dailys for the week
                    if (!dailys.Any())
                        continue;

                    // calculate WAU with available days
                    var sumRatings = dailys.Sum(row => row.Rating);
                    var weeklyAverage = sumRatings / dailys.Count();

                    RatingStatus status;
                    if (dailys.Any(d => d.Reliability >= 10))
                        status = RatingStatus.Rated;
                    else if (dailys.Any(d => d.Reliability > 0))
                        status = RatingStatus.Projected;
                    else
                        status = RatingStatus.Unrated;

                    updateList.Add(new
                    {
                        PlayerRatingId = playerDailys.Key,
                        Rating = weeklyAverage,
                        RatingStatus = status,
                        Type = "WeeklyAverage_Singles",
                        Date = now
                    });
                }

                connection.Execute("INSERT INTO WeeklyAverage (PlayerRatingId, Rating, Type, RatingStatus, Date) VALUES (@PlayerRatingId, @Rating, @Type, @RatingStatus, @Date)",
                    updateList, transaction);
                transaction.Commit();
            }
            catch (Exception e)
            {
                transaction.Rollback();
                EmailService.SendEmailNotification("chas@universaltennis.com", "Weekly Metrics Error Report", "Weekly Metrics Capture Failed");
                // Just so Hangfire marks as failed and shows the error
                throw new InvalidOperationException("Job failed to complete: " + e.Message);
            }
            finally
            {
                transaction.Dispose();
                connection.Close();
            }
        }
    }
}
