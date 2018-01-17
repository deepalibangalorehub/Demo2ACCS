using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using UniversalTennis.Algorithm.Models;
using UniversalTennis.Algorithm.Repository;
using UniversalTennis.Algorithm.Service;

namespace UniversalTennis.Algorithm
{
    public class AlgorithmDoubles
    {
        private readonly IPlayerService _playerService;
        private readonly IResultService _resultService;
        private readonly ILogger _logger;
        private readonly IRatingJobRepository _ratingJobRepository;
        private readonly Config _config;
        private RatingRule _ratingRule;

        private string _status;
        private int _ratingJobId;

        public AlgorithmDoubles (
            ILoggerFactory logger,
            IPlayerService playerService,
            IResultService resultService,
            IRatingJobRepository ratingJobRepository,
            IOptions<Config> config
        )
        {
            _logger = logger.CreateLogger("UniversalTennis.Algorithm.AlgorithmDoubles");
            _playerService = playerService;
            _resultService = resultService;
            _ratingJobRepository = ratingJobRepository;
            _config = config.Value;
        }

        private async Task LogStatus(string status)
        {
            _status = status;
            await _ratingJobRepository.UpdateStatusById(_ratingJobId, status);
        }

        private void LogException(Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }

        public string GetStatus()
        {
           return  _status;
        }

        public void SetRule(RatingRule rule)
        {
            _ratingRule = rule;
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task UpdateRating(int count,  RatingRule rule, int jobId)
        {
            // resolve any events
            await _playerService.ResolvePlayerEvents();
            // await _resultService.ResolveResultEvents();

            _ratingJobId = jobId;

            try
            {
                // always update the rule
                _ratingRule = rule ?? RatingRule.GetDefault("doubles", _config.ConnectionStrings.DefaultConnection);

                await LogStatus("Loading Players...");
                _logger.LogInformation("Retrieving Players...");
                await LogStatus("Loading players...");
                var resultThreshold = DateTime.UtcNow.AddMonths(-1 * int.Parse(_config.OldestResultInMonths));
                var players = (await _playerService.GetPlayersWithResults("doubles", resultThreshold)).ToDictionary(item => item.Id);
                // all doubles results in the last year
                _logger.LogInformation("Retrieving Results...");
                await LogStatus("Loading results...");
                var results = await _resultService.GetPlayerResultsFromYear("doubles", resultThreshold);

                _logger.LogInformation("Running calculations...");
                for (var i = 0; i < count; i++)
                {
                    await LogStatus($"Running calculations - Iteration {i + 1}");
                    DoCalc(players, i + 1, results);
                }

                _logger.LogInformation("Calculating Competitiveness...");
                await LogStatus("Calculating competitiveness...");

                foreach (var player in players)
                {
                    var ratingResults = LoadRatingResults(results[player.Value.Id]);
                    player.Value.Stats.CompetitiveMatchPctDoubles = CalculateCompetitiveness(ratingResults);
                }

                _logger.LogInformation("Correcting players...");
                await LogStatus("Correcting players...");

                NormalizeRatingsII(players.Values.ToList(), results);

                _logger.LogInformation("Saving Ratings");
                await LogStatus("Saving Ratings...");

                using (var connection = new SqlConnection(_config.ConnectionStrings.DefaultConnection))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {              
                        const string updateQuery = "Update PlayerRating Set FinalDoublesRating = @FinalDoublesRating, DoublesRating = @DoublesRating, DoublesReliability = @DoublesReliability," +
                                                   " CompetitiveMatchPctDoubles = @CompetitiveMatchPctDoubles, ActiveDoublesResults = @ActiveDoublesResults, DoublesBenchmarkRating = @DoublesBenchmarkRating" +
                                                   " PlayerGender = @Gender Where playerid = @Id";
                        foreach (var player in players)
                        {
                                connection.Execute(updateQuery, new
                                {
                                    DoublesRating = player.Value.Stats.DoublesRating,
                                    DoublesReliability = player.Value.Stats.DoublesReliability,
                                    FinalDoublesRating = player.Value.Stats.FinalDoublesRating,
                                    CompetitiveMatchPctDoubles = player.Value.Stats.CompetitiveMatchPctDoubles,
                                    DoublesBenchmarkRating = player.Value.Stats.DoublesBenchmarkRating,
                                    Id = player.Value.Id,
                                    Gender = player.Value.Gender,
                                    ActiveDoublesResults = player.Value.Stats.ActiveDoublesResults
                                }, transaction: transaction);

                        }
                        transaction.Commit();
                    }
                }

                // clean up players
                players = null;
                GC.Collect();

                await LogStatus("Checking for disconnected pools...");
                UpdateDisconnectedPools(results);

                await LogStatus("Completed");
                _logger.LogInformation("Algorithm Completed Successfully");
            }
            catch (Exception e)
            {
                LogException(e);
                _logger.LogInformation("Algorithm Failed");
                await LogStatus("Failed");
            }
            finally
            {
                // close the job
                var job = await _ratingJobRepository.GetById(_ratingJobId);
                job.EndTime = DateTime.Now;
                await _ratingJobRepository.Update(job);
            }
        }

        public List<Result> LoadRatingResults(List<Result> allResults, int maxCount = 30)
        {
            var rs = allResults.OrderByDescending(x => x.ResultDate);
            var ratingResults = rs.Where(r => r.ScoreIsValid()).Take(maxCount).ToList();
            if (rs.Count() > maxCount)
            {
                var extras = rs.Skip(maxCount).Where(x => x.ResultDate.Date == ratingResults.Last().ResultDate.Date).ToList();
                ratingResults.AddRange(extras);
            }
            return ratingResults;
        }

        public void DoCalc(Dictionary<int, Player> players, int iteration, IDictionary<int, List<Result>> dictPlayers)
        {
            foreach (var player in players)
            {
                if (!dictPlayers.ContainsKey(player.Value.Id)) continue;
                var playerResults = LoadRatingResults(dictPlayers[player.Value.Id]);
                if (playerResults.Count > 0)
                {
                    var newRatingInfo = CalculateDoublesUtr(playerResults, players, player.Value);
                    player.Value.Stats.CalculatedRating = double.IsNaN(newRatingInfo.Rating) ? 0d : newRatingInfo.Rating;
                    player.Value.Stats.CalculatedReliability = double.IsNaN(newRatingInfo.Reliability) ? 0 : newRatingInfo.Reliability;
                }
                else
                {
                    player.Value.Stats.CalculatedReliability = 0;
                }
            }
            foreach (var player in players)
            {
                player.Value.Stats.DoublesRating = player.Value.Stats.CalculatedRating;
                player.Value.Stats.DoublesReliability = player.Value.Stats.CalculatedReliability;
                if (player.Value.Stats.DoublesReliability > _ratingRule.provisionalDoublesReliabilityThreshold &&
                    player.Value.Stats.DoublesRating > 0)
                {
                    if (player.Value.Stats.DoublesBenchmarkRating == null)
                    {
                        player.Value.Stats.DoublesBenchmarkRating = player.Value.Stats.DoublesRating;
                    }
                    else
                    {
                        // trail the benchmark rating
                        player.Value.Stats.DoublesBenchmarkRating = TrailBenchmarkRating(
                            (double)player.Value.Stats.DoublesRating,
                            (double)player.Value.Stats.DoublesBenchmarkRating);
                    }
                }
            }
        }

        public double TrailBenchmarkRating(double rating, double benchmarkRating)
        {
            if (benchmarkRating < rating)
            {
                return Math.Max(benchmarkRating,
                    rating - _ratingRule.benchmarkTrailSpan);
            }
            else if (benchmarkRating > rating)
            {
                return Math.Min(benchmarkRating,
                    rating + _ratingRule.benchmarkTrailSpan);
            }
            return benchmarkRating;
        }

        public RatingInfoDoubles CalculateDoublesUtr(List<Result> results, Dictionary<int, Player> players, Player player)
        {
            var ratingInfo = new RatingInfoDoubles();
            double sumWeight = 0;
            double sumDynamicXWeight = 0;
            double sumPlayerReliabilityWeight = 0;
            int resultCount = 0;
            var eligibleResultsList = new List<int>();
            foreach (var result in results)
            {
                var winner1 = players[result.Winner1Id];
                var winner2 = players[result.Winner2Id ?? 0];
                var loser1 = players[result.Loser1Id];
                var loser2 = players[result.Loser2Id ?? 0];

                if (winner1 == null || winner2 == null || loser1 == null || loser2 == null)
                    continue;

                double w1Reliability;
                double w2Reliability;
                double l1Reliability;
                double l2Reliability;

                // this value should be pre-calculated
                var isCompetitiveResult =
                    result.Competitiveness.Equals("Competitive", StringComparison.OrdinalIgnoreCase);

                winner1.Stats.AssignedRating = AssignPlayerRatingForDoubles(winner1, winner2, loser1, loser2, isCompetitiveResult, out w1Reliability);
                winner2.Stats.AssignedRating = AssignPlayerRatingForDoubles(winner2, winner1, loser1, loser2, isCompetitiveResult, out w2Reliability);
                loser1.Stats.AssignedRating = AssignPlayerRatingForDoubles(loser1, loser2, winner1, winner2, isCompetitiveResult, out l1Reliability);
                loser2.Stats.AssignedRating = AssignPlayerRatingForDoubles(loser2, loser1, winner1, winner2, isCompetitiveResult, out l2Reliability);

                // skip result if someone cannot be assigned
                if (winner1.Stats.AssignedRating == null ||
                    winner2.Stats.AssignedRating == null ||
                    loser1.Stats.AssignedRating == null ||
                    loser2.Stats.AssignedRating == null)
                {
                    continue;
                }

                winner1.Stats.AssignedReliability = w1Reliability;
                winner2.Stats.AssignedReliability = w2Reliability;
                loser1.Stats.AssignedReliability = l1Reliability;
                loser2.Stats.AssignedReliability = l2Reliability;

                TeamInfo playerTeam;
                TeamInfo opponentTeam;

                // identify player's team
                if (player.Id == winner1.Id || player.Id == winner2.Id)
                {
                    playerTeam = new TeamInfo(winner1, winner2, false);
                    opponentTeam = new TeamInfo(loser1, loser2, true);
                }
                else
                {
                    playerTeam = new TeamInfo(loser1, loser2, false);
                    opponentTeam = new TeamInfo(winner1, winner2, true);
                }

                if (opponentTeam.TeamReliability <= 0) continue;

                var dynamic = CalculateDynamicRating(player.Id, playerTeam, opponentTeam, result);
                dynamic = Math.Truncate(1000 * dynamic) / 1000;
                var numPartnerResults = GetPartnerResults(playerTeam, results);
                var matchWeights = CalculateMatchWeight(playerTeam, opponentTeam, result, numPartnerResults);

                if (matchWeights.MatchWeight >= _ratingRule.eligibleResultsWeightThreshold)
                    eligibleResultsList.Add(result.Id);                   

                sumDynamicXWeight += (dynamic * matchWeights.MatchWeight);
                sumWeight += matchWeights.MatchWeight;
                // divide by interpool coefficient so it doesn't skew player reliability
                sumPlayerReliabilityWeight += (matchWeights.MatchWeight / matchWeights.InterpoolCoeffecient);

                resultCount++;
            }
            // store active doubles results
            player.Stats.ActiveDoublesResults = JsonConvert.SerializeObject(eligibleResultsList.ToArray());

            var n = resultCount;
            // if no valid results, don't calculate
            if (n <= 0)
            {
                ratingInfo.Rating = 0;
                ratingInfo.Reliability = 0;
                return ratingInfo;
            }

            var doublesUtr = (sumWeight > 0) ? (sumDynamicXWeight / sumWeight) : 1d;
            var doublesReliability = players[player.Id].Stats.AssignedReliability;
            var singlesReliabilityAsPercent = ((player.Stats.RatingReliability ?? 0) / 10);
            var doublesReliabilityAsPercent = (doublesReliability / 10);

            // Determination of Player’s (Singles) UTR and corresponding Player’s (singles) UTR Reliability. Factor in if available
            var singlesWeight = player.Stats.RatingReliability >= _ratingRule.singlesWeightReliabilityThreshold
                ? _ratingRule.singlesWeightOnDoubles
                : 0;
            if (player.Stats.FinalRating > 0 && player.Stats.RatingReliability > 0)
                ratingInfo.Rating = ((n * (doublesUtr * doublesReliabilityAsPercent)) + (singlesWeight * ((player.Stats.FinalRating ?? 0) * singlesReliabilityAsPercent))) / ((n * doublesReliabilityAsPercent) + (singlesWeight * singlesReliabilityAsPercent));
            else
                ratingInfo.Rating = (n * (doublesUtr * doublesReliabilityAsPercent)) / (n * doublesReliabilityAsPercent);

            ratingInfo.Reliability = CalculatePlayerReliability(sumPlayerReliabilityWeight);

            return ratingInfo;
        }

        public double? AssignPlayerRatingForDoubles(Player player, Player partner, Player op1, Player op2, bool competitive, out double reliability)
        {
            reliability = 0.0;
            var t = _ratingRule.provisionalSinglesReliabilityThreshold;
            var dt = _ratingRule.provisionalDoublesReliabilityThreshold;
            /*
             * Figure out which rating to use in the calculation
             */
            if (player.Stats.DoublesRating > 0 && player.Stats.DoublesReliability >= dt)
            {
                reliability = player.Stats.DoublesReliability ?? 0;
                return player.Stats.DoublesRating ?? 0; // use Doubles UTR  
            }
            if (player.Stats.FinalRating > 0 && player.Stats.RatingReliability >= t)
            {
                // use Singles UTR         
                reliability = player.Stats.RatingReliability ?? 0;
                return player.Stats.FinalRating ?? 0;
            }
            // TODO: Use provisional DUTR
            if (partner.Stats.DoublesRating > 0 && partner.Stats.DoublesReliability >= dt)
            {
                // use partners DUTR
                reliability = partner.Stats.DoublesReliability ?? 0;
                return partner.Stats.DoublesRating ?? 0;
            }
            // TODO: use partners provisional DUTR
            // TODO: use average provisional DUTR of opponents
            if (partner.Stats.FinalRating > 0 && partner.Stats.RatingReliability >= t)
            {
                // use partner's singles UTR
                reliability = partner.Stats.RatingReliability ?? 0;
                return partner.Stats.FinalRating ?? 0;
            }
            if (op1.Stats.DoublesRating > 0 && op1.Stats.DoublesReliability >= dt && op2.Stats.DoublesRating >= dt &&
                op2.Stats.DoublesReliability > 0)
            {
                // use opponents team DUTR
                reliability = ((op1.Stats.DoublesReliability ?? 0) + (op2.Stats.DoublesReliability ?? 0)) / 2;
                return ((op1.Stats.DoublesRating ?? 0) + (op2.Stats.DoublesRating ?? 0)) / 2;
            }
            if (op1.Stats.DoublesRating > 0 && op1.Stats.DoublesReliability >= dt)
            {
                // use available DUTR of opponents
                reliability = op1.Stats.DoublesReliability ?? 0;
                return (op1.Stats.DoublesRating ?? 0);
            }
            if (op2.Stats.DoublesRating > 0 && op2.Stats.DoublesReliability >= dt)
            {
                reliability = op2.Stats.DoublesReliability ?? 0;
                return (op2.Stats.DoublesRating ?? 0);
            }

            // Only assign singles rating where match is competitive
            if (!competitive && _ratingRule.EnableCompetitivenessFilter)
                return null;

            if (op1.Stats.FinalRating > 0 && op1.Stats.RatingReliability >= t && op2.Stats.FinalRating > 0 &&
                op2.Stats.RatingReliability >= t)
            {
                // use average single UTR of opp.
                reliability = ((op1.Stats.RatingReliability ?? 0) + (op2.Stats.RatingReliability ?? 0)) / 2;
                return ((op1.Stats.FinalRating ?? 0) + (op2.Stats.FinalRating ?? 0)) / 2;
            }
            if (op1.Stats.FinalRating > 0 && op1.Stats.RatingReliability >= t)
            {
                // use available single UTR of opponents
                reliability = op1.Stats.RatingReliability ?? 0;
                return (op1.Stats.FinalRating ?? 0);
            }
            if (op2.Stats.FinalRating > 0 && op2.Stats.RatingReliability >= t)
            {
                reliability = op2.Stats.RatingReliability ?? 0;
                return (op2.Stats.FinalRating ?? 0);
            }
            return null;
        }

        private double CalculatePlayerReliability(double sumOfResultReliability)
        {
            var playerReliability = 1 + (sumOfResultReliability / 6);
            return (playerReliability > _ratingRule.MaxReliability) ? _ratingRule.MaxReliability : playerReliability; // Max ralibility is limited in the rating rules
        }

        #region DynamicRatings
        public double CalculateDynamicRating(int playerId, TeamInfo playerTeam, TeamInfo opponentTeam, Result result)
        {
            // Effect of Partner-UTR-Spread on Team Adjustment Factor
            var matchPartnersDelta = playerTeam.RatingDiff - opponentTeam.RatingDiff;
            var baseline = CalculateMatchBaseline(playerTeam.TeamRating, opponentTeam.TeamRating);
            var teamAdjustment = CalculateTeamAdjustmentFactor(playerId, result, matchPartnersDelta, baseline, GetGenderType(playerTeam, opponentTeam));
            var teamDynamicRating = CalculateTeamDynamicRating(baseline, teamAdjustment);

            // determine which position player is in
            if (playerId == playerTeam.Player1Id)
            {
                // calc dynamic rating as if partner rating was the same and cap there
                var playerTeamDynamicRating = CalculateTeamDynamicRating(CalculateMatchBaseline(playerTeam.Player1Rating, opponentTeam.TeamRating), teamAdjustment);
                var dynamicCap = (playerTeamDynamicRating);
                var dynamic = (teamDynamicRating - playerTeam.TeamRating) + playerTeam.Player1Rating;
                dynamic = Clamp(dynamic, 1, dynamicCap);
                if (_ratingRule.EnableDynamicRatingCap)
                {
                    return playerTeam.Player1Gender.ToLower() == "m" ? Math.Min(16.5f, dynamic) : Math.Min(13.5f, dynamic);
                }
                // cap dynamic
                return dynamic;
            }
            if (playerId == playerTeam.Player2Id)
            {
                var playerTeamDynamicRating = CalculateTeamDynamicRating(CalculateMatchBaseline(playerTeam.Player2Rating, opponentTeam.TeamRating), teamAdjustment);
                var dynamicCap = (playerTeamDynamicRating);
                var dynamic = (teamDynamicRating - playerTeam.TeamRating) + playerTeam.Player2Rating;
                dynamic = Clamp(dynamic, 1, dynamicCap);
                if (_ratingRule.EnableDynamicRatingCap)
                {
                    return playerTeam.Player2Gender.ToLower() == "m" ? Math.Min(16.5f, dynamic) : Math.Min(13.5f, dynamic);
                }
                return dynamic;
            }
            throw new RatingException("Could not map player to team");
        }

        public Result.MatchGender GetGenderType(TeamInfo team1, TeamInfo team2)
        {
            var genders = new List<string>() { team1.Player1Gender, team1.Player2Gender, team2.Player1Gender, team2.Player2Gender };
            if (genders.Contains("M") && genders.Contains("F"))
                return Result.MatchGender.Coed;
            if (genders.Contains("M"))
                return Result.MatchGender.Male;
            if (genders.Contains("F"))
                return Result.MatchGender.Female;
            throw new RatingException("Unable to determine result gender type");
        }

        public double CalculateTeamDynamicRating(double baseline, double adjustment)
        {
            return Clamp(baseline + adjustment, 1f, 16.5f);
        }

        public double CalculateMatchBaseline(double team1Dutr, double team2Dutr)
        {
            var baseline = (team1Dutr + team2Dutr) / 2;
            return Math.Truncate(100 * baseline) / 100;
        }

        public double CalculateTeamAdjustmentFactor(int playerId, Result result, double matchPartnerDelta, double baseline, Result.MatchGender genderType)
        {
            var pctGamesWon = result.PercentOfGamesWon(playerId);
            var scaleGraduationMale = _ratingRule.maleUTRScaleGraduationLow +
                                      ((baseline - 1) * (_ratingRule.maleUTRScaleGraduationHigh - _ratingRule.maleUTRScaleGraduationLow)) /
                                      15.5d;
            scaleGraduationMale = Clamp(scaleGraduationMale, _ratingRule.maleUTRScaleGraduationLow,
                _ratingRule.maleUTRScaleGraduationHigh);
            var scaleGraduationFemale = _ratingRule.femaleUTRScaleGraduationLow +
                                        ((baseline - 1) * (_ratingRule.femaleUTRScaleGraduationHigh - _ratingRule.femaleUTRScaleGraduationLow)) /
                                        12.5d;
            scaleGraduationFemale = Clamp(scaleGraduationFemale, _ratingRule.femaleUTRScaleGraduationLow,
                _ratingRule.femaleUTRScaleGraduationHigh);
            switch (genderType)
            {
                case Result.MatchGender.Coed:
                    var scaleAvg = (scaleGraduationFemale + scaleGraduationMale) / 2;
                    var adjustmentCoed = (pctGamesWon * 2 * scaleAvg) - scaleAvg;
                    return (_ratingRule.partnerSpreadAdjustmentFactor * matchPartnerDelta) + adjustmentCoed;
                case Result.MatchGender.Male:
                    var adjustmentMale = (pctGamesWon * 2 * scaleGraduationMale) - scaleGraduationMale;
                    return (_ratingRule.partnerSpreadAdjustmentFactor * matchPartnerDelta) + adjustmentMale;
                case Result.MatchGender.Female:
                    var adjustmentFemale = (pctGamesWon * 2 * scaleGraduationFemale) - scaleGraduationFemale;
                    return (_ratingRule.partnerSpreadAdjustmentFactor * matchPartnerDelta) + adjustmentFemale;
                default:
                    throw new RatingException("Unable to calculate team adjustment factor");
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            var result = value;
            if (value.CompareTo(max) > 0)
                result = max;
            if (value.CompareTo(min) < 0)
                result = min;
            return result;
        }
        #endregion

        #region Match Weight
        public RatingInfoDoubles.WeightingFactors CalculateMatchWeight(TeamInfo playerTeam, TeamInfo opponentTeam, Result r, int numPartnerResults)
        {
            /*
            * Match Weight is A x B x C x D * E * F
            * Where:
            * A = Opponent's Player Rating Reliability
            * B = Parter Frequency
            * C = Match Competitiveness Factor
            * D = Opponent Benchmark Reliability Factor
            * E = Interpool Reliability Factor
            * F = Team Rating Reliability
            */

            var a = (_ratingRule.EnableMatchFormatReliability) ? CalculateMatchFormatReliabilityFactor(r) : 10.0f;
            var b = _ratingRule.EnablePartnerFrequencyReliability ? CalculatePartnerFrequency(numPartnerResults) : 1.0f;

            var c = (_ratingRule.EnableMatchCompetitivenessCoeffecient) ? MatchCompetivenessCalculator.CalculateMatchCompetivenessCoeffecient(playerTeam, opponentTeam, r, _ratingRule) : 1.0f;
            var d = 1.0f; // not yet used
            var e = (_ratingRule.EnableInterpoolCoeffecient) ? CalculateInterpoolCoefficient(playerTeam, opponentTeam) : 1.0f;
            var f = CalculateOpponentRatingReliabilityFactor(opponentTeam);
            var matchWeight = a * b * c * d * e * f;

            return new RatingInfoDoubles.WeightingFactors
            {
                MatchFormatReliability = a,
                MatchFrequencyReliability = b,
                MatchCompetitivenessCoeffecient = c,
                BenchmarkMatchCoeffecient = d,
                OpponentRatingReliability = f,
                InterpoolCoeffecient = e,
                MatchWeight = Math.Truncate(100000 * matchWeight) / 100000
            };
        }

        private double CalculatePartnerFrequency(double n)
        {
            var d = (1 - _ratingRule.minPartnerFrequencyFactor);
            return _ratingRule.minPartnerFrequencyFactor + (d / n);
        }

        public int GetPartnerResults(TeamInfo team, List<Result> results)
        {
            var count = 0;
            foreach (var r in results)
            {
                var team1Ids = new List<int> { r.Winner1Id, r.Winner2Id ?? 0 };
                var team2Ids = new List<int> { r.Loser1Id, r.Loser2Id ?? 0 };
                if (team1Ids.Contains(team.Player1Id) && team1Ids.Contains(team.Player2Id))
                    count++;
                if (team2Ids.Contains(team.Player1Id) && team2Ids.Contains(team.Player2Id))
                    count++;
            }
            return count > 0 ? count : 1;
        }

        private float CalculateMatchFormatReliabilityFactor(Result r)
        {
            if (!r.IsDNF)
            {
                switch (r.MatchType)
                {
                    case Result.MatchFormat.BestOfFiveSets:
                        return _ratingRule.BestOfFiveSetReliability;
                    case Result.MatchFormat.BestOfThreeSets:
                        return _ratingRule.BestOfThreeSetReliability;
                    case Result.MatchFormat.EightGameProSet:
                        return _ratingRule.EightGameProSetReliability;
                    case Result.MatchFormat.MiniSet:
                        return _ratingRule.MiniSetReliability;
                    case Result.MatchFormat.OneSet:
                        return _ratingRule.OneSetReliability;
                    default:
                        throw new ArgumentException("Match type " + r.MatchType + " is not one of the supported MatchFormat", "matchType");
                }
            }
            switch (r.CompletedSets)
            {
                case 0:
                    return 0.0f;
                case 1:
                    return 0.5f;
                case 2:
                    return 0.8f;
                case 3:
                    return 0.9f;
                case 4:
                    return 0.95f;
                default:
                    throw new ArgumentException("Completed set count: " + r.CompletedSets + " out of range", "completedSets");
            }
        }

        private float CalculateInterpoolCoefficient(TeamInfo playerTeam, TeamInfo opponentTeam)
        {
            // any diff in country ids
            var isForeignMatch = playerTeam.Player1CountryId != opponentTeam.Player1CountryId ||
                                 playerTeam.Player1CountryId != opponentTeam.Player2CountryId ||
                                 playerTeam.Player2CountryId != opponentTeam.Player2CountryId;

            if (playerTeam.HasCollegePlayer ^ opponentTeam.HasCollegePlayer)
                return _ratingRule.interpoolCoefficientCollege;

            return isForeignMatch ? _ratingRule.interpoolCoefficientCountry : 1.0f;
        }

        private static double CalculateOpponentRatingReliabilityFactor(TeamInfo opponentTeam)
        {
            // mean of opponent reliabilities
            return opponentTeam.TeamReliability;
        }

        #endregion

        public double CalculateCompetitiveness(List<Result> playerResults)
        {
            var oneYear = DateTime.Now.Date.AddYears(-1);
            var competitiveCount = 0;
            var sortResults =
                playerResults.Where(r => r.ResultDate > DateTime.Now.AddYears(-1))
                    .OrderByDescending(r => r.ResultDate);
            var max = sortResults.Count() < 30 ? sortResults.Count() : 30;
            foreach (var r in sortResults.Take(max))
            {
                if (r.ResultDate > oneYear)
                {
                    var setCount = r.WinnerSets.Count(i => i != 0);
                    var lossScore = r.LoserSets.Sum();
                    var winSet1 = r.WinnerSets[0];
                    var losSet1 = r.LoserSets[0];
                    var isComp = false;
                    if (setCount == 1)
                    {
                        switch (winSet1)
                        {
                            case 10:
                                isComp = (double)losSet1 / 10 > 0.5;
                                break;
                            case 9:
                            case 8:
                                isComp = (double)losSet1 / 8 > 0.5;
                                break;
                            case 7:
                            case 6:
                                isComp = (double)losSet1 / 6 > 0.5;
                                break;
                            case 5:
                            case 4:
                                isComp = (double)losSet1 / 4 > 0.5;
                                break;
                        }
                    }
                    else if (setCount <= 3)
                    {
                        isComp = (double)lossScore / 12 > 0.5;
                    }
                    else
                    {
                        isComp = (double)lossScore / 18 > 0.5;
                    }
                    if (isComp)
                    {
                        competitiveCount++;
                    }
                }
            }
            return max == 0 ? 0 : (int)Math.Round(((double)competitiveCount / max) * 100);
        }

        /*
        public void NormalizeRatingsI(List<Player> players)
        {
            try
            {
                var topMale =
                    players.Where(
                            p => p.Sex.Equals("m", StringComparison.OrdinalIgnoreCase) && p.DoublesReliability == 10)
                        .OrderByDescending(p => p.DoublesRating)
                        .Select(p => p.DoublesRating)
                        .First();
                var topFemale =
                players.Where(
                        p => p.Sex.Equals("f", StringComparison.OrdinalIgnoreCase) && p.DoublesReliability == 10)
                    .OrderByDescending(p => p.DoublesRating)
                    .Select(p => p.DoublesRating)
                    .First();
                foreach (var p in players)
                {
                    if (p.Sex.Equals("m", StringComparison.OrdinalIgnoreCase))
                    {
                        p.NormalizedDoublesRating = (p.DoublesRating*(16.5/topMale));
                    }
                    else
                    {
                        p.NormalizedDoublesRating = (p.DoublesRating * (13.5/topFemale));
                    }
                    p.NormalizedDoublesRating = SmoothRating(p, p.NormalizedDoublesRating);
                }
            }
            catch (Exception e)
            {
                throw new RatingException("There was a problem saving normalized ratings: " + e.Message);
            }
        }
        */

        public void NormalizeRatingsII(List<Player> players, IDictionary<int, List<Result>> results)
        {
            var rule = _ratingRule;
            var curve = rule.NormalizationCurve;

            foreach (var p in players)
            {
                var percentAgainstCollege = GetPercentCollegeNationality(LoadRatingResults(results[p.Id]), rule);

                var highLevel = Math.Ceiling((decimal)(p.Stats.DoublesRating ?? 0));
                var lowLevel = Math.Floor((decimal)(p.Stats.DoublesRating ?? 0));

                var collegeAdjustment = 0f;
                var nationalAdjustment = 0f;

                if (p.Gender.Equals("m", StringComparison.OrdinalIgnoreCase))
                {
                    // to correct for the 16 - 16.5 range case
                    var adj = lowLevel >= 16 ? 15.75f : Convert.ToInt32(lowLevel);

                    var fw = lowLevel == 0 ? 0 : curve.CollegeCorrectionsMale[lowLevel.ToString()];
                    var cw = highLevel == 0 ? 0 : curve.CollegeCorrectionsMale[highLevel.ToString()];

                    collegeAdjustment = fw + (((float)(p.Stats.DoublesRating ?? 0) - adj) * (cw - fw));

                    var fw2 = lowLevel == 0 ? 0 : curve.NonCollegeCorrectionsMale[lowLevel.ToString()];
                    var cw2 = highLevel == 0 ? 0 : curve.NonCollegeCorrectionsMale[highLevel.ToString()];
                    nationalAdjustment = fw2 + (((float)(p.Stats.DoublesRating ?? 0) - adj) * (cw2 - fw2));
                }
                else
                {
                    var adj = lowLevel >= 13 ? 12.75f : Convert.ToInt32(lowLevel);

                    var fw = lowLevel == 0 ? 0 : curve.CollegeCorrectionsFemale[lowLevel.ToString()];
                    var cw = highLevel == 0 ? 0 : curve.CollegeCorrectionsFemale[highLevel.ToString()];
                    collegeAdjustment = fw + (((float)(p.Stats.DoublesRating ?? 0) - adj) * (cw - fw));

                    var fw2 = lowLevel == 0 ? 0 : curve.NonCollegeCorrectionsFemale[lowLevel.ToString()];
                    var cw2 = highLevel == 0 ? 0 : curve.NonCollegeCorrectionsFemale[highLevel.ToString()];
                    nationalAdjustment = fw2 + (((float)(p.Stats.DoublesRating ?? 0) - adj) * (cw2 - fw2));
                }

                var collegeCorrection = collegeAdjustment +
                                        ((1 - percentAgainstCollege) * (nationalAdjustment - collegeAdjustment));
                var finalrating = Convert.ToDouble(p.Stats.DoublesRating + collegeCorrection);
                finalrating = SmoothRating(p, finalrating);
                p.Stats.FinalDoublesRating = Math.Truncate(finalrating * 100) / 100;

                /*
                connection.Execute(@"Update Player Set DoublesReliability = 0, DoublesRating = NULL, FinalDoublesRating = NULL, RatingStatusDoubles = 'Unrated'
                                    WHERE doublesrating is not null and doublesrating > 0 
                                    and (id not in (select Winner1Id from result where ResultDate >= DATEADD(year,-1,GETDATE()) and teamType = 'd')  
                                    and id not in (select Loser1Id from result where ResultDate >= DATEADD(year,-1,GETDATE()) and teamType = 'd') 
                                    and id not in (select Winner2Id from result where ResultDate >= DATEADD(year,-1,GETDATE()) and teamType = 'd') 
                                    and id not in (select Loser2Id from result where ResultDate >= DATEADD(year,-1,GETDATE()) and teamType = 'd'))", commandTimeout: 1200);
                                    */
            }
        }

        private static double SmoothRating(Player p, double? rating)
        {
            if (rating > 14.5 && p.Stats.DoublesReliability < 10 &&
                p.Gender.Equals("m", StringComparison.OrdinalIgnoreCase))
            {
                return (double)((double)(rating - 14.5d) * (p.Stats.DoublesReliability / 10) + 14.5);
            }
            if (rating > 11.5 && p.Stats.DoublesReliability < 10 &&
                p.Gender.Equals("f", StringComparison.OrdinalIgnoreCase))
            {
                return (double)((double)(rating - 11.5d) * (p.Stats.DoublesReliability / 10) + 11.5);
            }
            if (rating < 1)
                return 1d;
            return rating ?? 1d;
        }

        private float GetPercentCollegeNationality(List<Result> allResults, RatingRule rule)
        {
            var importIds = new[] { 6, 8, 14, 16, 36, 37, 24, 7 };  // TODO: hardcoded import types
            var importSubTypes = new[] { "LTATour", "AustraliaAMT" };
            var total = 0;
            var totalCollege = 0;

            if (allResults == null) // skip if player has no  results
            {
                return 0;
            }

            allResults = allResults.OrderByDescending(x => x.ResultDate).ToList();

            foreach (var result in allResults)
            {
                total++;
                if (result.DataImportTypeId != null)
                {
                    if (importIds.Contains((int) result.DataImportTypeId) || importSubTypes.Contains(result.DataImportSubType))
                    {
                        totalCollege++;
                    }
                }
            }
            return total <= 0 ? 0 : (float)totalCollege / total;
        }

        private void UpdateDisconnectedPools(IDictionary<int, List<Result>> results)
        {
            var graph = SanityCheck.FindDoublesGroups(results, _ratingRule.disconnectedPoolThreshold);
            var disconnectedPlayerIds = new List<int>();
            foreach (var pool in graph)
            {
                disconnectedPlayerIds.AddRange(pool.Nodes);
            }
            var idList = disconnectedPlayerIds.ToArray();
            var idSubLists = new List<int[]>();
            // break update list into batches of 2000 since mssql has a param cap of 2100
            for (var i = 0; i < (float)idList.Length / 2000; i++)
            {
                var sub = idList.Skip(i * 2000).Take(2000);
                idSubLists.Add(sub.ToArray());
            }
            using (var connection = new SqlConnection(_config.ConnectionStrings.DefaultConnection))
            {
                foreach (var sl in idSubLists)
                {
                    connection.Execute(@"Update PlayerRating Set FinalDoublesRating = 0, DoublesRating = 0, DoublesReliability = 0 WHERE playerid in @Disconnected",
                        new { Disconnected = sl }, commandTimeout: 600);
                }
            }
        }
    }
}