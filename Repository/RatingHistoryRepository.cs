using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversalTennis.Algorithm.Data;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Repository
{
    public class RatingHistoryRepository : IRatingHistoryRepository
    {
        private readonly UniversalTennisContext _context;

        public RatingHistoryRepository(
            UniversalTennisContext context
        )
        {
            _context = context;
        }

        public IEnumerable<WeeklyAverage> GetAllWeeklyByPlayerId(int playerId)
        {
            return _context.WeeklyAverages.AsNoTracking()
                .Where(w => w.PlayerRating.PlayerId == playerId && w.Type == "WeeklyAverage_Singles");
        }

        public IEnumerable<WeeklyAverage> GetAllWeeklyByPlayerId(int playerId, RatingStatus ratingStatus)
        {
            return _context.WeeklyAverages.AsNoTracking()
                .Where(w => w.PlayerRating.PlayerId == playerId && w.Type == "WeeklyAverage_Singles")
                .Where(w => w.RatingStatus == ratingStatus);
        }

        public WeeklyAverage GetWeeklyOnDateByPlayerId(int playerId, DateTime date)
        {
            return _context.WeeklyAverages.AsNoTracking()
                .FirstOrDefault(w => w.PlayerRating.PlayerId == playerId
                    && w.Date.Date <= date 
                    && w.Date.Date >= date.AddDays(-7)
                    && w.Type == "WeeklyAverage_Singles"
            );
        }

        public WeeklyAverage GetWeeklyOnDateByPlayerId(int playerId, DateTime date, RatingStatus ratingStatus)
        {
            return _context.WeeklyAverages.AsNoTracking()
                .FirstOrDefault(w => w.PlayerRating.PlayerId == playerId 
                    && w.Date.Date <= date 
                    && w.Date.Date >= date.AddDays(-7)
                    && w.Type == "WeeklyAverage_Singles"
                    && w.RatingStatus == ratingStatus
                );
        }

        public async Task DeleteDailyRatingByPlayerId(int playerId)
        {
            var history = _context.DailyRatings.Include(w => w.PlayerRating)
                .Where(w => w.PlayerRating.PlayerId == playerId);
            _context.DailyRatings.RemoveRange(history);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteWeeklyAverageByPlayerId(int playerId)
        {
            var history = _context.WeeklyAverages
                .Include(w => w.PlayerRating)
                .Where(w => w.PlayerRating.PlayerId == playerId); ;
            _context.WeeklyAverages.RemoveRange(history);
            await _context.SaveChangesAsync();
        }

        public List<DailyRating> MergeDailyRatings(int[] sourcePlayerIds, int targetPlayerRatingId)
        {
            var mergedDailyRatings = new List<DailyRating>();
            if (sourcePlayerIds.Length > 0)
            {
                // Get daily ratings for source players
                _context.Database.SetCommandTimeout(120);
                var dailys = _context.DailyRatings.Where(d => sourcePlayerIds.Contains(d.PlayerRating.PlayerId)).ToList();

                // nothing to merge
                if (!dailys.Any())
                    return mergedDailyRatings;

                // get list of distinct dates
                var dateList = (
                    from row in dailys.AsEnumerable()
                    select row.Date
                ).Distinct();

                // for each distinct date, average the dailys and store in target player
                foreach (var dt in dateList)
                {
                    // Calculate the new dailys for each date
                    var selectedRows = dailys.Where(d => d.Date == dt).ToList();

                    var mergedDaily = CalculateMergedDailyRating(selectedRows);
                    var mergedReliability = selectedRows.Max(row => (float) row.Reliability);

                    // Create the new rows TODO: hardcoded alg type
                    var newRow = new DailyRating()
                    {
                        PlayerRatingId = (int)targetPlayerRatingId,
                        Rating = double.IsNaN(mergedDaily) ? 0 : mergedDaily,
                        Reliability = float.IsNaN(mergedReliability) ? 0 : mergedReliability,
                        Algorithm = "V3_Singles",
                        Date = dt
                    };
                    mergedDailyRatings.Add(newRow);                  
                }
                _context.DailyRatings.AddRange(mergedDailyRatings);
                // delete the old rows
                _context.DailyRatings.RemoveRange(dailys);
                _context.SaveChanges();
            }
            return mergedDailyRatings;
        }

        public static double CalculateMergedDailyRating(IReadOnlyCollection<DailyRating> dailyRatings)
        {
            // take weighted average based on reliability %
            var sumDailys = dailyRatings.Sum(row => (float)row.Rating*(row.Reliability/10));
            return sumDailys / dailyRatings.Sum(row => row.Reliability / 10);
        }

        public void MergeWeeklyAverage(int[] sourcePlayerIds, int targetPlayerRatingId, List<DailyRating> targetDailys)
        {
            // Get weekly averages for source players
            _context.Database.SetCommandTimeout(120);
            var weeklys =
                _context.WeeklyAverages.Where(
                    r =>
                        sourcePlayerIds.Contains(r.PlayerRating.PlayerId)).ToList();

            // nothing to merge
            if (!weeklys.Any())
                return;

            // get list of distinct dates
            var dateList = (
                from row in weeklys
                select row.Date
            ).Distinct();

            // for each distinct date, re-calculte the wau using the merged dailys
            foreach (var dt in dateList)
            {
                // get dailys in the week prior
                var dailyDict = targetDailys.Where(d => d.Date >= dt.Date.AddDays(-7) && d.Date <= dt.Date)
                    .GroupBy(d => d.PlayerRatingId).ToList();

                foreach (var playerDailys in dailyDict)
                {
                    var dailys = playerDailys.Where(d => d.Algorithm.Equals("V3_Singles")).ToList();

                    // don't insert anything if the player has no dailys for the week
                    if (!dailys.Any())
                        continue;

                    // calculate WAU with available dailys
                    var sumRatings = dailys.Sum(row => row.Rating);
                    var weeklyAverage = sumRatings / dailys.Count;

                    RatingStatus status;
                    if (dailys.Any(d => d.Reliability >= 10))
                        status = RatingStatus.Rated;
                    else if (dailys.Any(d => d.Reliability > 0))
                        status = RatingStatus.Projected;
                    else
                        status = RatingStatus.Unrated;

                    var newWeekly = new WeeklyAverage()
                    {
                        PlayerRatingId = targetPlayerRatingId,
                        RatingStatus = status,
                        Rating = weeklyAverage,
                        Type = "WeeklyAverage_Singles",
                        Date = dt.Date
                    };
                    _context.WeeklyAverages.Add(newWeekly);
                }
            }
            // remove originals
            _context.WeeklyAverages.RemoveRange(weeklys);
            _context.SaveChanges();
        }

        public DailyRating GetDailyratingForPlayer(int playerId, DateTime date, string algorithmType)
        {
            return _context.DailyRatings.FirstOrDefault(x => x.PlayerRating.PlayerId == playerId && x.Date == date.Date && x.Algorithm == algorithmType);
        }
    }
}
