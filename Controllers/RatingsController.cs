using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using UniversalTennis.Algorithm.Helpers;
using UniversalTennis.Algorithm.Jobs;
using UniversalTennis.Algorithm.Models;
using UniversalTennis.Algorithm.Repository;
using UniversalTennis.Algorithm.Service;

namespace UniversalTennis.Algorithm.Controllers
{
    [Route("api/[controller]")]
    public class RatingsController : Controller
    {
        private readonly IPlayerService _playerService;
        private readonly IResultService _resultService;
        private readonly IRatingHistoryRepository _ratingHistoryRepository;
        private readonly ISubRatingRepository _subRatingRepository;
        private readonly IRatingHistoryService _ratingHistoryService;
        private static IServiceProvider _serviceProvider;
        private readonly Config _config;

        public RatingsController(
            IPlayerService playerService,
            IResultService resultService,
            IRatingHistoryRepository ratingHistoryRepository,
            IServiceProvider serviceProvider,
            IRatingHistoryService ratingHistoryService,
            ISubRatingRepository subRatingRepository,
            IOptions<Config> config
        )
        {
            _resultService = resultService;
            _playerService = playerService;
            _ratingHistoryRepository = ratingHistoryRepository;
            _ratingHistoryService = ratingHistoryService;
            _serviceProvider = serviceProvider;
            _subRatingRepository = subRatingRepository;
            _config = config.Value;
        }

        [HttpGet("distribution", Name = "distribution")]
        public ActionResult GetRatingDistribution(string type)
        {
            if (type.Equals("singles"))
                return new ObjectResult(_playerService.GetSinglesDistribution());
            if (type.Equals("doubles"))
                return new ObjectResult(_playerService.GetDoublesDistribution());
            return StatusCode(400, "Type must be singles or doubles");
        }

        [HttpGet("saveweekly", Name = "saveweekly")]
        public ActionResult SaveWeeklyHistory()
        {
            BackgroundJob.Enqueue(() => _ratingHistoryService.SaveWeeklyAverage("WeeklyAverage_Singles", "V3_Singles"));
            return StatusCode(400, "Started");
        }

        [HttpGet("historyforplayer", Name = "historyforplayer")]
        public ActionResult GetHistoryForPlayer(int playerId, string status, DateTime? date)
        {
            if (date == null)
            {
                // filter by status if provided
                var was = status == null
                    ? _ratingHistoryRepository.GetAllWeeklyByPlayerId(playerId)
                    : _ratingHistoryRepository.GetAllWeeklyByPlayerId(playerId, RatingsHelper.RatingStatusFromString(status));
                // return list of history
                return new JsonResult(
                    was.Select(w => new { w.Date, w.Rating, w.RatingStatus }),
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.None }
                );
            }
            // filter by status if provided
            var wa = status == null
                ? _ratingHistoryRepository.GetWeeklyOnDateByPlayerId(playerId, (DateTime)date)
                : _ratingHistoryRepository.GetWeeklyOnDateByPlayerId(playerId, (DateTime)date, RatingsHelper.RatingStatusFromString(status));
            if (wa == null)
            {
                return StatusCode(204);
            }
            return new JsonResult(
                new {wa.Date, wa.Rating, wa.RatingStatus},
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.None }
            );
        }

        [HttpGet("playerratingondate")]
        public ActionResult GetPlayerRatingOnDate(int playerid, DateTime date, string algorithmType)
        {
            var dailyRating = _ratingHistoryRepository.GetDailyratingForPlayer(playerid, date, algorithmType);
            return new JsonResult(
                dailyRating,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.None }
            );
        }

        [HttpGet("playersubratings")]
        public ActionResult GetPlayerSubRatingsAndRankings(int playerid)
        {
            var dailyRating = _subRatingRepository.GetPlayerSubRatingsAndRankings(playerid);
            return new JsonResult(
                dailyRating,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.None }
            );
        }

        [HttpGet("ratingbreakdown", Name = "ratingbreakdown")]
        public async Task<SinglesRatingCalculation> GetPlayerRatingBreakdown(int playerId)
        {
            var resultsTask = _resultService.GetPlayerResultsFromYear(playerId, "singles", DateTime.Now.AddYears(-12));
            var algorithmInstance = (Algorithm)_serviceProvider.GetService(typeof(Algorithm));
            var resultCalculations = new List<SinglesResultCalculation>();
            var rule = RatingRule.GetDefault("singles", _config.ConnectionStrings.DefaultConnection);
            var results = await resultsTask;

            // get distinct player ids
            var playerIds = results
                .Select(r => new [] {r.Winner1Id, r.Loser1Id})
                .SelectMany(r => r).Distinct().ToArray();

            var playerRepo = await _playerService.GetPlayerInfo(playerIds);
            var playerDict = playerRepo.ToDictionary(x => x.Id);
            var player = playerDict[playerId];

            var resultsArray = algorithmInstance.GetRatingResults(player, playerDict, results, rule);
            var opponentsMatchFrequency = algorithmInstance.CalculateMatchFrequencyReliability(player.Id, resultsArray);

            // do basic ratings calc
            foreach (Result result in resultsArray)
            {
                var rs = new SinglesResultCalculation { Score = result.ScoreHtml };
                Player opponent;
                if (player.Id == result.Winner1Id)
                {
                    opponent = playerDict[result.Loser1Id];
                    rs.WinLoss = "W";
                }
                else
                {
                    opponent = playerDict[result.Winner1Id];
                    rs.WinLoss = "L";
                }
                rs.OpponentName = opponent.DisplayName;
                rs.OpponentReliability = opponent.Stats.Reliability;
                rs.OpponentRating = opponent.Stats.Rating;
                rs.WeightingFactors = algorithmInstance.CalculateMatchResultRelibilityFactor
                    (player, result, opponent, opponentsMatchFrequency[opponent.Id], false, rule);
                rs.DynamicRating = algorithmInstance.CalculateDynamicRating(player, opponent, result, playerDict, rule);
                resultCalculations.Add(rs);
            }
            var ratingInfo = algorithmInstance.CalculatePlayerUTR(player, resultsArray, playerDict, rule);
            return new SinglesRatingCalculation
            {
                Rating = ratingInfo.Rating,
                Reliability = ratingInfo.Reliability,
                ResultCalculations = resultCalculations
            };
        }

        [HttpGet("ratingbreakdowndoubles", Name = "ratingbreakdowndoubles")]
        public async Task<DoublesRatingCalculation> GetPlayerRatingBreakdownDoubles(int playerId)
        {
            var resultsTask = _resultService.GetPlayerResultsFromYear(playerId, "doubles", DateTime.Now.AddYears(-12));
            var algorithmInstance = (AlgorithmDoubles)_serviceProvider.GetService(typeof(AlgorithmDoubles));
            var resultCalculations = new List<DoublesResultCalculation>();
            var rule = RatingRule.GetDefault("doubles", _config.ConnectionStrings.DefaultConnection);
            var results = await resultsTask;

            // get distinct player ids
            var playerIds = results
                .Select(r => new[] { r.Winner1Id, r.Loser1Id, r.Winner2Id, r.Loser2Id })
                .SelectMany(r => r).Distinct().ToArray();

            var playerRepo = await _playerService
                .GetPlayerInfo(Array.ConvertAll(playerIds, value => (int) value));
            var playerDict = playerRepo.ToDictionary(x => x.Id);
            var player = playerDict[playerId];

            foreach (var p in playerRepo)
            {
                p.Stats.AssignedRating = p.Stats.DoublesRating ?? 0;
                p.Stats.AssignedReliability = p.Stats.DoublesReliability ?? 0;
            }

            algorithmInstance.SetRule(rule);

            var resultsArray = algorithmInstance.LoadRatingResults(results, rule.NumberOfResults);

            foreach (var result in resultsArray)
            {
                var rs = new DoublesResultCalculation { Score = result.ScoreHtml };
                TeamInfo playerTeam, opponentTeam;
                Player partner, opp1, opp2;
                if (player.Id == result.Winner1Id || player.Id == result.Winner2Id)
                {
                    partner = player.Id == result.Winner1Id
                        ? playerDict[(int) result.Winner2Id]
                        : playerDict[result.Winner1Id];
                    playerTeam = new TeamInfo(player, partner, false);
                    opp1 = playerDict[result.Loser1Id];
                    opp2 = playerDict[(int)result.Loser2Id];
                    opponentTeam = new TeamInfo(opp1, opp2, true);
                    rs.WinLoss = "W";
                }
                else
                {
                    partner = player.Id == result.Loser1Id
                        ? playerDict[(int)result.Loser2Id]
                        : playerDict[result.Loser1Id];
                    playerTeam = new TeamInfo(player, partner, false);
                    opp1 = playerDict[result.Winner1Id];
                    opp2 = playerDict[(int) result.Winner2Id];
                    opponentTeam = new TeamInfo(opp1, opp2, true);
                    rs.WinLoss = "L";
                }
                rs.PartnerName = partner.DisplayName;
                rs.Opponent1Name = opp1.DisplayName;
                rs.Opponent2Name = opp2.DisplayName;
                rs.OpponentTeam = opponentTeam;
                rs.PlayerTeam = playerTeam;
                var partnerFrequency = algorithmInstance.GetPartnerResults(rs.PlayerTeam, results);
                rs.WeightingFactors = algorithmInstance.CalculateMatchWeight(rs.PlayerTeam, rs.OpponentTeam, result, partnerFrequency);
                rs.DynamicRating = algorithmInstance.CalculateDynamicRating(player.Id, rs.PlayerTeam, rs.OpponentTeam,
                    result);
                var matchPartnersDelta = rs.PlayerTeam.RatingDiff - rs.OpponentTeam.RatingDiff;
                var gendertype = algorithmInstance.GetGenderType(rs.PlayerTeam, rs.OpponentTeam);
                var baseline = algorithmInstance.CalculateMatchBaseline(rs.PlayerTeam.TeamRating,
                    rs.OpponentTeam.TeamRating);
                rs.TeamDynamicRating = algorithmInstance.CalculateTeamDynamicRating(baseline,
                    algorithmInstance.CalculateTeamAdjustmentFactor(playerId, result, matchPartnersDelta, baseline, gendertype));
                resultCalculations.Add(rs);
            }
            var ratingInfo = algorithmInstance.CalculateDoublesUtr(results, playerRepo.ToDictionary(item => item.Id), player);
            return new DoublesRatingCalculation
            {
                Rating = ratingInfo.Rating,
                Reliability = ratingInfo.Reliability,
                ResultCalculations = resultCalculations
            };
        }
    }
}
