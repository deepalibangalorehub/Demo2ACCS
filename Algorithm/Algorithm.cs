using Dapper;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UniversalTennis.Algorithm.Models;
using UniversalTennis.Algorithm.Repository;
using UniversalTennis.Algorithm.Service;

namespace UniversalTennis.Algorithm
{
    public class Algorithm
    {
        private readonly IPlayerService _playerService;
        private readonly IResultService _resultService;
        private readonly ILogger _logger;
        private readonly IRatingJobRepository _ratingJobRepository;
        private readonly ISubRatingRepository _subRatingRepository;
        private readonly Config _config;

        private string _status;
        private int _ratingJobId;

        public Algorithm (
            ILoggerFactory logger,
            IPlayerService playerService,
            IResultService resultService,
            ISubRatingRepository subRatingRepository,
            IRatingJobRepository ratingJobRepository,
            IOptions<Config> config
        )
        {
            _logger = logger.CreateLogger("UniversalTennis.Algorithm.Algorithm");
            _playerService = playerService;
            _resultService = resultService;
            _subRatingRepository = subRatingRepository;
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

        /*
         * Calculate the rating for all players N times
         */
        [AutomaticRetry(Attempts = 0)]
        public async Task UpdateRating(int count, bool asAlternate, int jobId)
        {
            // resolve any events
            await _playerService.ResolvePlayerEvents();
            // await _resultService.ResolveResultEvents();

            _ratingJobId = jobId;
            var conn = new SqlConnection(_config.ConnectionStrings.DefaultConnection);
            SqlTransaction transaction = null;
            RatingRule rule = RatingRule.GetDefault("singles", _config.ConnectionStrings.DefaultConnection);

            try
            {
                _logger.LogInformation("Started Singles Algorithm - {0} iterations", count);
                
                var resultThreshold = DateTime.UtcNow.AddMonths(-1 * int.Parse(_config.OldestResultInMonths));
                _logger.LogInformation("Retrieving Players...");
                await LogStatus("Loading players...");
                var players =  
                    (await _playerService.GetPlayersWithResults("singles", resultThreshold)).ToDictionary(item => item.Id);

                _logger.LogInformation("Retrieving Results...");
                await LogStatus("Loading results...");
             
                var playerResults = await _resultService.GetPlayerResultsFromYear("singles", resultThreshold);

                _logger.LogInformation("Running calculations...");
                await DoCalculation(count, players, rule, playerResults);

                _logger.LogInformation("Calculating Competitiveness...");
                await LogStatus("Calculating competitiveness...");

                foreach (var row in players.Values)
                {
                    if (playerResults == null || playerResults.Count <= 0) continue;
                    var player = row;
                    var results = GetRatingResults(player, players, playerResults[player.Id], rule);
                    var comp = CalculateCompetitiveness(results);
                    row.Stats.CompetitiveMatchPct = comp.Item1;
                    row.Stats.RoutineMatchPct = comp.Item2;
                    row.Stats.DecisiveMatchPct = comp.Item3;
                }

                _logger.LogInformation("Correcting players...");
                await LogStatus("Correcting players...");

                DoPostProcessing(playerResults, players.Values.ToList(), rule);

                _logger.LogInformation("Saving Sub Ratings");
                await LogStatus("Saving Sub Ratings...");
                foreach (var player in players.Values)
                {
                    // update sub rating
                    if (player.Stats.SubRating != null)
                    {
                        await _subRatingRepository.AddOrUpdateSubRating(player.Stats.SubRating);
                    }
                }

                _logger.LogInformation("Saving Ratings");
                await LogStatus("Saving Ratings...");

                conn.Open();
                transaction = conn.BeginTransaction();

                foreach (var player in players.Values)
                {
                    conn.Execute(@"update playerrating set finalrating = @FinalRating, actualrating = @ActualRating, ratingreliability = @RatingReliability, " +
                                 "competitiveMatchPct = @CompetitiveMatchPct, routineMatchPct = @RoutineMatchPct, decisiveMatchPct = @DecisiveMatchPct, " +
                                 "inactiveRating = NULL, activeSinglesResults = @ActiveSinglesResults, playergender = @Gender where playerId = @Id", 
                    new {
                            Id = player.Id,
                            CompetitiveMatchPct = player.Stats.CompetitiveMatchPct,
                            RoutineMatchPct = player.Stats.RoutineMatchPct,
                            DecisiveMatchPct = player.Stats.DecisiveMatchPct,
                            RatingReliability = player.Stats.RatingReliability,
                            ActualRating = player.Stats.ActualRating,
                            FinalRating = player.Stats.FinalRating,
                            Gender = player.Gender,
                            ActiveSinglesResults = player.Stats.ActiveSinglesResults
                        }, transaction: transaction);
                }

                transaction.Commit();

                // clean up the players
                players = null;
                GC.Collect();

                UpdateDisconnectedPools(rule, playerResults);

                await LogStatus("Completed");
                _logger.LogInformation("Algorithm Completed Successfully");
            }
            catch (Exception e)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch (Exception e2)
                {
                    LogException(e2);
                }  
                LogException(e);
                _logger.LogError("Algorithm Failed");
                await LogStatus("Failed");
            }
            finally
            {
                transaction?.Dispose();
                conn.Close();
                // close the job
                var job = await _ratingJobRepository.GetById(_ratingJobId);
                job.EndTime = DateTime.Now;
                await _ratingJobRepository.Update(job);
            }
        }

        /*
         * Do the rating calculation for each player the number of times specified
         */
        public async Task<Dictionary<int, Player>> DoCalculation(int count, Dictionary<int, Player> players, RatingRule rule, Dictionary<int, List<Result>> playerResults)
        {
            for (int i = 0; i < count; i++)
            {
                await LogStatus($"Running calculations - Iteration {i + 1}");
                foreach (var player in players.Values)
                {     
                    // update progress counters                  
                    int playerId = player.Id; //Get Player ID
                    ArrayList results = GetRatingResults(player, players, playerResults[playerId], rule); //Get the results of the player's matches
                    RatingInfo rating = new RatingInfo();
                    if (results != null && results.Count > 0) // skip player if they have no results
                    {
                        rating = CalculatePlayerUTR(player, results, players, rule);
                    }
                    else //No recent result case, this needs to be improved to place players in an inactive & currated state
                    {
                        rating.Rating = (float)player.Stats.Rating;
                        rating.Reliability = 0;
                    }

                    player.Stats.ActualRating = Math.Truncate(rating.Rating * 100) / 100; // store actual value, even for benchmarks
                    player.Stats.RatingReliability = rating.Reliability;
                    player.Stats.SubRating = rating.SubRating;
                    player.Stats.Rating = rating.Rating;
                    player.Stats.Reliability = rating.Reliability;
                }
            }
            return players;
        }

        /*
        * Get the matches for the player that are going to be used for calculating UTR
        */
        public ArrayList GetRatingResults(Player playerInfo, Dictionary<int, Player> players, List<Result> allResults, RatingRule rule)
        {
            if (allResults == null) // skip if player has no  results
            {
                return null;
            }
            allResults = new List<Result>(allResults.OrderByDescending(r => r.ResultDate));
            ArrayList results = new ArrayList();
            Result previousResult = new Result();
            var maxResults = rule.NumberOfResults;
            foreach (Result result in allResults)
            {
                if (result.ResultDate > rule.ResultThreshold) // Last 30 or Last Year (variable based on rating rules)
                {
                    int compare = DateTime.Compare(result.ResultDate, previousResult.ResultDate);
                    if ((results.Count < maxResults) || (compare == 0))
                    {
                        var opponentId = GetOpponentId(playerInfo.Id, result);
                        var opponentInfo = players[opponentId];
                        if (opponentInfo.Stats.Reliability > 0 && result.ScoreIsValid())
                        {
                            results.Add(result);
                            previousResult = result;
                        }
                        // include extra results for each lopsided match
                        if (Math.Abs(playerInfo.Stats.Rating - opponentInfo.Stats.Rating) > 2.5) maxResults += 1;
                    }
                }
            }
            return results;
        }

        public RatingInfo CalculatePlayerUTR(Player player, ArrayList results, Dictionary<int, Player> playerRepo, RatingRule rule)
        {
            RatingInfo playerUTR = new RatingInfo(); // store player's UTR and reliability
            ArrayList resultRatingInfoList = new ArrayList(); // stores the RatingInfo for each match
            var activeResultsIds = new List<int>();
            float SumRatingXReliability = 0;
            float SumReliabilityRating = 0;
            float SumReliabilityPlayerReliability = 0;
            //stores unique opponent IDs and number of times played
            Dictionary<int, int> opponentsMatchFrequency = CalculateMatchFrequencyReliability(player.Id, results);          
            foreach (Result r in results)
            {
                // Calculate Dynamcic Rating and Match Result Reliability Factor for each match, store in list
                var ri = DoResultsCalculation(player, r, results, playerRepo, rule, opponentsMatchFrequency);
                resultRatingInfoList.Add(ri);
                if (ri.weightingFactors.MatchWeight >= rule.eligibleResultsWeightThreshold) activeResultsIds.Add(r.Id);
            }
            // Store active results used in calculation
            player.Stats.ActiveSinglesResults = JsonConvert.SerializeObject(activeResultsIds.ToArray());
            foreach (RatingInfo ri in resultRatingInfoList)
            {
                SumRatingXReliability += ri.Rating * ri.Reliability; // For each match, Dynmic Ratch x Match Reliability
                SumReliabilityRating += ri.Reliability; // Sum of Match Reliabilities

                // divide by interpool coefficient so it doesn't skew player reliability
                SumReliabilityPlayerReliability += ri.Reliability / ri.weightingFactors.InterpoolCoeffecient;
            }
            float rating = (SumReliabilityRating == 0.0) ? 0 : (SumRatingXReliability / SumReliabilityRating);
            playerUTR.Rating = (float)SmoothRating(rating); // Player rating is these number divided
            playerUTR.Rating = (float)Math.Truncate(100 * playerUTR.Rating) / 100;
            playerUTR.Reliability = CalculatePlayerReliability(SumReliabilityPlayerReliability, rule);
            if (float.IsNaN(playerUTR.Rating))
            {
                System.Diagnostics.Debug.WriteLine(playerUTR.Rating);
            }
            if (player.IsTop700())
            {
                CalculatePlayerSubUTRs(resultRatingInfoList, playerUTR, player.Stats.Id);
            }
            return playerUTR;
        }

        public void CalculatePlayerSubUTRs(ArrayList resultRatingInfoList, RatingInfo ratingInfo, int playerRatingId)
        {
            ratingInfo.SubRating = new SubRating
            {
                PlayerRatingId = playerRatingId,
                ResultCount = resultRatingInfoList.Count
            };
            ratingInfo.SubRating.HardCourt = CalculatePlayerSurfaceUTR(resultRatingInfoList, SurfaceType.Hard, out int hardCount);
            ratingInfo.SubRating.HardCourtCount = hardCount;
            ratingInfo.SubRating.ClayCourt = CalculatePlayerSurfaceUTR(resultRatingInfoList, SurfaceType.Clay, out int clayCount);
            ratingInfo.SubRating.ClayCourtCount = clayCount;
            ratingInfo.SubRating.GrassCourt = CalculatePlayerSurfaceUTR(resultRatingInfoList, SurfaceType.Grass, out int grassCount);
            ratingInfo.SubRating.GrassCourtCount = grassCount;
            ratingInfo.SubRating.SixWeek = CalculatePlayerTimeUTR(DateTime.Now.AddDays(-42), resultRatingInfoList, out int sixCount);
            ratingInfo.SubRating.SixWeekCount = sixCount;
            ratingInfo.SubRating.EightWeek = CalculatePlayerTimeUTR(DateTime.Now.AddDays(-56), resultRatingInfoList, out int eightCount);
            ratingInfo.SubRating.EightWeekCount = eightCount;
            ratingInfo.SubRating.OneMonth = CalculatePlayerTimeUTR(DateTime.Now.AddMonths(-1), resultRatingInfoList, out int oneCount);
            ratingInfo.SubRating.OneMonthCount = oneCount;
            ratingInfo.SubRating.ThreeMonth = CalculatePlayerTimeUTR(DateTime.Now.AddMonths(-3), resultRatingInfoList, out int threeCount);
            ratingInfo.SubRating.ThreeMonthCount = threeCount;
            ratingInfo.SubRating.GrandSlamMasters = CalculateAtpMastersUTR(resultRatingInfoList, out int gmCount);
            ratingInfo.SubRating.GrandSlamMastersCount = gmCount;
        }

        public double? CalculatePlayerSurfaceUTR(ArrayList resultRatingInfoList, SurfaceType surfaceType, out int matchCount)
        {
            var sumRatingXReliability = 0d;
            var sumReliabilityRating = 0d;
            matchCount = 0;
            foreach (RatingInfo ri in resultRatingInfoList)
            {
                // get the surface weight and use in place of normal match weight
                var surfaceWeight = 0;
                if (ri.SurfaceType == surfaceType)
                {
                    surfaceWeight = 1;
                    matchCount++;
                }
                var weightedMatchWeight = ri.Reliability * surfaceWeight;
                sumRatingXReliability += ri.Rating * weightedMatchWeight;
                sumReliabilityRating += weightedMatchWeight;
            }
            return sumReliabilityRating <= 0.0 ? (double?) null : sumRatingXReliability / sumReliabilityRating;      
        }

        public double? CalculateAtpMastersUTR(ArrayList resultRatingInfoList, out int matchCount)
        {
            var sumRatingXReliability = 0d;
            var sumReliabilityRating = 0d;
            matchCount = 0;
            foreach (RatingInfo ri in resultRatingInfoList)
            {
                var eventWeight = 0;
                if (ri.IsMastersOrGrandslam ?? false)
                {
                    matchCount++;
                    eventWeight = 1;
                }
                var weightedMatchWeight = ri.Reliability * eventWeight;
                sumRatingXReliability += ri.Rating * weightedMatchWeight;
                sumReliabilityRating += weightedMatchWeight;
            }
            return sumReliabilityRating <= 0.0 ? (double?)null : sumRatingXReliability / sumReliabilityRating;
        }

        public double? CalculatePlayerTimeUTR(DateTime threshold, ArrayList resultRatingInfoList, out int matchCount)
        {
            var sumRatingXReliability = 0d;
            var sumReliabilityRating = 0d;
            matchCount = 0;
            foreach (RatingInfo ri in resultRatingInfoList)
            {
                var monthWeight = 0;
                if (ri.Date >= threshold)
                {
                    monthWeight = 1;
                    matchCount++;
                }
                // get the month weight and use in place of normal match weight
                var weightedMatchWeight = ri.Reliability * monthWeight;
                sumRatingXReliability += ri.Rating * weightedMatchWeight;
                sumReliabilityRating += weightedMatchWeight;
            }
            return sumReliabilityRating <= 0.0 ? (double?)null : sumRatingXReliability / sumReliabilityRating;
        }

        public RatingInfo DoResultsCalculation(Player playerInfo, Result r, ArrayList results, Dictionary<int, Player> playerRepo, RatingRule rule, Dictionary<int, int> matchFrequency)
        {
            RatingInfo ratingInfo = new RatingInfo
            {
                SurfaceType = r.SurfaceType,
                Date = r.ResultDate,
                IsMastersOrGrandslam = r.IsMastersOrGrandslam
            }; 
            var opponent = playerRepo[GetOpponentId(playerInfo.Id, r)]; //info about opponent
            //var opponent = GetOpponent(playerInfo.Id, r, playerRepo);
            if (opponent.BenchmarkRating != null && opponent.BenchmarkRating > 0)
            {
                opponent.Stats.Rating = (double)opponent.BenchmarkRating;
            }
            ratingInfo.Rating = CalculateDynamicRating(playerInfo, opponent, r, playerRepo, rule);
            if (float.IsNaN(ratingInfo.Rating))
            {
                System.Diagnostics.Debug.WriteLine(ratingInfo.Rating);
            }

            ratingInfo.AgainstBenchmark = (opponent.Stats.IsBenchmark) ? true : false;
            float opponentFrequency = 0;
            try
            {
                opponentFrequency = (float)matchFrequency[opponent.Id]; // value from dictionary
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, opponent.Id.ToString());
            }
            RatingInfo.WeightingFactors wf = CalculateMatchResultRelibilityFactor(playerInfo, r, opponent, opponentFrequency, ratingInfo.AgainstBenchmark, rule);
            ratingInfo.Reliability = wf.MatchWeight; //reliability for match
            ratingInfo.weightingFactors = wf;
            return ratingInfo;
        }

        public float CalculatePlayerReliability(float sumOfResultReliability, RatingRule rule)
        {
            float playerReliability = (rule.ReliabilityWeight + sumOfResultReliability) / rule.ReliabilityWeight;
            return (playerReliability > rule.MaxReliability) ? rule.MaxReliability : playerReliability; // Max ralibility is limited in the rating rules
        }

        #region DynamicRating
        public float CalculateDynamicRating(Player playerInfo, Player opponent, Result r, Dictionary<int, Player> playerRepo, RatingRule rule)
        {
            var baseline = CalculateMatchBaseline(r, playerRepo);
            var af = CalculateMatchAdjustmentFactor(playerInfo, opponent.Gender, baseline, r, rule);
            if (double.IsNaN(af) || double.IsNaN(baseline))
            {
                System.Diagnostics.Debug.WriteLine(af);
            }
            var dynamicRating = (float)baseline + (float)af;
            if (dynamicRating < 1f)
            {
                dynamicRating = 1f;
            }
            if (rule.EnableDynamicRatingCap)
            {
                dynamicRating = playerInfo.Gender.ToLower() == "m" ? Math.Min(16.5f, dynamicRating) : Math.Min(13.5f, dynamicRating);
            }
            return (float)Math.Truncate(1000 * dynamicRating) / 1000;
        }

        public double CalculateMatchBaseline(Result r, Dictionary<int, Player> playerRepo)
        {
            var winner = playerRepo[r.Winner1Id];
            var loser = playerRepo[r.Loser1Id];

            // use benchmark rating if set
            if (winner.BenchmarkRating != null && winner.BenchmarkRating > 0)
                winner.Stats.Rating = (double)winner.BenchmarkRating;
            if (loser.BenchmarkRating != null && loser.BenchmarkRating > 0)
                loser.Stats.Rating = (double)loser.BenchmarkRating;

            // new player criteria (actual rating should be 0)
            bool winnerIsNewPlayer = winner.Stats.Rating <= 0.0f && winner.Stats.Reliability <= 1.0f;
            bool loserIsNewPlayer = loser.Stats.Rating <= 0.0f && loser.Stats.Reliability <= 1.0f;

            if (winnerIsNewPlayer && loserIsNewPlayer)
            {
                // TODO: check if provisional rating in result (not storing this anywhere yet)
                return 1.0d;
            }
            if (winnerIsNewPlayer && !loserIsNewPlayer)
            {
                // if winner is new player, use the loser's rating
                return (float)Math.Round(loser.Stats.Rating, 1);
            }
            if (!winnerIsNewPlayer && loserIsNewPlayer)
            {
                return (float)Math.Round(winner.Stats.Rating, 1);
            }
            double baseline = (winner.Stats.Rating + loser.Stats.Rating) / 2;

            return (float)Math.Round(baseline, 2);
        }

        //Adjustment Factor (same sex) = [(%Games Won * 2 * X) - X] where X = "UTR Scale Graduation"
        //Adjustment Factor (male vs female) = [((%Games Won * (X + Y)) - ((X + Y)/2)] where X,Y = "UTR Scale Graduation M/F"
        public double CalculateMatchAdjustmentFactor(Player playerInfo, string opponentGender, double baseline, Result r, RatingRule rule)
        {
            // use college scale factors if it's a college match
            var scaleHighMale = r.IsCollegeMatch ? rule.maleUTRCollegeScaleGraduationHigh : rule.maleUTRScaleGraduationHigh;
            var scaleLowMale = r.IsCollegeMatch ? rule.maleUTRCollegeScaleGraduationLow : rule.maleUTRScaleGraduationLow;
            var scaleHighFemale = r.IsCollegeMatch ? rule.femaleUTRCollegeScaleGraduationHigh : rule.femaleUTRScaleGraduationHigh;
            var scaleLowFemale = r.IsCollegeMatch ? rule.femaleUTRCollegeScaleGraduationLow : rule.femaleUTRScaleGraduationLow;

            var pctGamesWon = r.PercentOfGamesWon(playerInfo.Id);
            // if baseline is 1, scale grad low should be used. if 16.5, scale grad high used
            // sliding linear scale for baselines in between
            var scaleGraduationMale = scaleLowMale +
                                      ((baseline - 1) * (scaleHighMale - scaleLowMale)) /
                                      15.5d;
            scaleGraduationMale = Clamp(scaleGraduationMale, scaleLowMale,
                scaleHighMale);
            var scaleGraduationFemale = scaleLowFemale +
                                        ((baseline - 1) * (scaleHighFemale - scaleLowFemale)) /
                                        15.5d;
            scaleGraduationFemale = Clamp(scaleGraduationFemale, scaleLowFemale,
                scaleHighFemale);
            if (playerInfo.Gender.Equals(opponentGender))
            {
                if (opponentGender.ToLower().Equals("m"))
                {
                    var adjM = (pctGamesWon * 2 * scaleGraduationMale) - scaleGraduationMale;
                    if (r.IsCollegeMatch && r.Loser1Id == playerInfo.Id && baseline < rule.maleScaleLossPercentageMaxLevel)
                        adjM = adjM * rule.maleUTRCollegeScaleLossPercentage;
                    return adjM;
                }
                var adjF = (pctGamesWon * 2 * scaleGraduationFemale) - scaleGraduationFemale;
                if (r.IsCollegeMatch && r.Loser1Id == playerInfo.Id && baseline < rule.femaleScaleLossPercentageMaxLevel)
                    adjF = adjF * rule.femaleUTRCollegeScaleLossPercentage;
                return adjF;
            }
            // Male vs Female - use average          
            var scaleGraduation = (scaleGraduationFemale + scaleGraduationMale) / 2;
            return (pctGamesWon * 2 * scaleGraduation) - scaleGraduation;
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

        #region ReliabilityFactor
        public RatingInfo.WeightingFactors CalculateMatchResultRelibilityFactor(Player player, Result r, Player opponent, float opponentFrequency, bool againstBenchmark, RatingRule rule)
        {
            /*
             * Match Result Reliabiliy Factor is A x B x C x D
             * Where:
             * A = Opponent's Player Rating Reliability
             * B = Match Format Reliability
             * C = Match Frequency Reliability
             * D = Match Competitiveness Coeffecient
             * E = Benchmark Match Coeffecient
             * F = Interpool Match Coeffecient
             */

            float a, b, c, d, rr, f;
            bool collegeInterpoolApplied = false;
            a = (rule.EnableOpponentRatingReliability) ? CalculateOpponentRatingReliability(opponent) : 10.0f;
            b = (rule.EnableMatchFormatReliability) ? CalculateMatchFormatReliability(r, rule) : 1.0f;
            c = (rule.EnableMatchFrequencyReliability) ? 2 / (opponentFrequency + 1) : 1.0f;
            d = (rule.EnableMatchCompetitivenessCoeffecient) ? MatchCompetivenessCalculator.CalculateMatchCompetivenessCoeffecient(player, opponent, r, rule) : 1.0f;
            f = (rule.EnableInterpoolCoeffecient) ? CalculateInterpoolCoefficient(player, opponent, rule, out collegeInterpoolApplied) : 1.0f;
            rr = a * b * c * d * f;
            float coef = 1.0f;
            if (againstBenchmark)
            {
                coef = (rule.EnableBenchmarkMatchCoeffecient) ? rule.BenchmarkMatchCoeffecient : 1.0f;
                rr = rr * coef;
            }

            RatingInfo.WeightingFactors wf = new RatingInfo.WeightingFactors();
            wf.MatchFormatReliability = b;
            wf.MatchFrequencyReliability = c;
            wf.MatchCompetitivenessCoeffecient = d;
            wf.BenchmarkMatchCoeffecient = coef;
            wf.OpponentRatingReliability = a;
            wf.InterpoolCoeffecient = f;
            // TESTING: use college interpool as match weight if it is applied
            //wf.MatchWeight = collegeInterpoolApplied ? (rule.interpoolCoefficientCollege*10) : (float)Math.Truncate(100000 * rr) / 100000;
            wf.MatchWeight = (float)Math.Truncate(100000 * rr) / 100000;
            return wf;
        }

        /*
         * Match Relibility coefficient is 1.0 for 3 and 5 set matches, 0.7 for pro set, 0.4 for miniset
         */
        public float CalculateMatchFormatReliability(Result r, RatingRule rule)
        {
            if (!r.IsDNF)
            {
                switch (r.MatchType)
                {
                    case Result.MatchFormat.BestOfFiveSets:
                        return rule.BestOfFiveSetReliability;
                    case Result.MatchFormat.BestOfThreeSets:
                        return rule.BestOfThreeSetReliability;
                    case Result.MatchFormat.EightGameProSet:
                        return rule.EightGameProSetReliability;
                    case Result.MatchFormat.MiniSet:
                        return rule.MiniSetReliability;
                    case Result.MatchFormat.OneSet:
                        return rule.OneSetReliability;
                    default:
                        throw new ArgumentException("Match type " + r.MatchType + " is not one of the supported MatchFormat", "matchType");
                }
            }
            else
            {
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
        }

        public float CalculateOpponentRatingReliability(Player opponent)
        {
            //Wrapper incase change is needed
            return (float)opponent.Stats.Reliability;
        }

        public Dictionary<int, int> CalculateMatchFrequencyReliability(int playerId, ArrayList results)
        {
            Dictionary<int, int> opponentCounts = new Dictionary<int, int>();
            foreach (Result r in results)
            {
                int opponentId = GetOpponentId(playerId, r);
                int count;
                opponentCounts.TryGetValue(opponentId, out count);
                opponentCounts[opponentId] = count + 1;
            }
            return opponentCounts;
        }

        public Dictionary<int, int> CalculateMatchFrequencyReliability(int playerId, List<Result> results)
        {
            Dictionary<int, int> opponentCounts = new Dictionary<int, int>();
            foreach (var r in results)
            {
                int opponentId = GetOpponentId(playerId, r);
                int count;
                opponentCounts.TryGetValue(opponentId, out count);
                opponentCounts[opponentId] = count + 1;
            }
            return opponentCounts;
        }
        #endregion

        public float CalculateInterpoolCoefficient(Player player, Player opponent, RatingRule rule, out bool collegeInterpoolApplied)
        {
            var isForeign = (player.CountryId != opponent.CountryId) && player.CountryId > 0 && opponent.CountryId > 0;
            var playerIsCollege = player.CollegeId > 0;
            var opponentIsCollege = opponent.CollegeId > 0;
            if (playerIsCollege && opponentIsCollege)
            {
                collegeInterpoolApplied = false;
                return 1.0f;
            }
            if (playerIsCollege ^ opponentIsCollege) // college vs non college
            {
                collegeInterpoolApplied = true;
                return rule.interpoolCoefficientCollege;
            }
            collegeInterpoolApplied = false;
            return isForeign ? rule.interpoolCoefficientCountry : 1.0f;
        }

        private static int GetOpponentId(int playerId, Result r)
        {
            return r.Winner1Id == playerId ? r.Loser1Id : r.Winner1Id;
        }

        public double SmoothRating(double rating)
        {
            return rating < 1d ? 1d : rating;
        }

        public double SmoothRating(string playerSex, double playerReliability, double rating)
        {
            if (rating > 14.5 && playerReliability < 10 &&
                playerSex.Equals("m", StringComparison.OrdinalIgnoreCase))
            {
                return (rating - 14.5d) * (playerReliability / 10) + 14.5;
            }
            if (rating > 11.5 && playerReliability < 10 &&
                playerSex.Equals("f", StringComparison.OrdinalIgnoreCase))
            {
                return (rating - 11.5d) * (playerReliability / 10) + 11.5;
            }
            return rating < 1d ? 1d : rating;
        }

        public Tuple<double, double, double> CalculateCompetitiveness(ArrayList results)
        {
            var playerResults = results.Cast<Result>()
                .ToList();
            var oneYear = DateTime.Now.Date.AddYears(-1);
            var competitiveCount = 0;
            var routineCount = 0;
            var decisiveCount = 0;
            var sortResults =
                playerResults.Where(r => r.ResultDate > oneYear)
                    .OrderByDescending(r => r.ResultDate);
            var max = sortResults.Count() < 30 ? sortResults.Count() : 30;
            foreach (var r in sortResults.Take(max))
            {
                if (r.ResultDate > oneYear)
                {
                    var setCount = r.WinnerSets.Count(i => i != 0);
                    var lossScore = r.LoserSets.Sum();
                    var winSet1 = r.WinnerSets[0];
                    var gamesNeedToWin = 0;
                    if (setCount == 1) // one set match
                    {
                        switch (winSet1)
                        {
                            case 10:
                                gamesNeedToWin = 10;
                                break;
                            case 9:
                            case 8:
                                gamesNeedToWin = 8;
                                break;
                            case 7:
                            case 6:
                                gamesNeedToWin = 6;
                                break;
                            case 5:
                            case 4:
                                gamesNeedToWin = 4;
                                break;
                        }
                    }
                    else if (setCount <= 3) // 3 set match
                    {
                        gamesNeedToWin = 12;
                    }
                    else // 1 set match
                    {
                        gamesNeedToWin = 18;
                    }
                    var lossPct = (float)lossScore / gamesNeedToWin;
                    if (lossPct > 0.5)
                        competitiveCount++;
                    else if (lossPct >= 0.26)
                        routineCount++;
                    else
                        decisiveCount++;
                }

            }
            return new Tuple<double, double, double>
            (
                max == 0 ? 0 : (int)Math.Round(((double)competitiveCount / max) * 100),
                max == 0 ? 0 : (int)Math.Round(((double)routineCount / max) * 100),
                max == 0 ? 0 : (int)Math.Round(((double)decisiveCount / max) * 100)
            );
        }

        public void DoPostProcessing(Dictionary<int, List<Result>> playerResults, List<Player> players, RatingRule rule)
        {
            var curve = rule.NormalizationCurve;

            foreach (var p in players)
            {
                var percentAgainstCollege = GetPercentCollegeNationality(playerResults[p.Id], rule);

                double rating;
                double reliability;

                
                rating = p.Stats.ActualRating ?? 0;
                reliability = p.Stats.RatingReliability ?? 0;
                

                var highLevel = Math.Ceiling((decimal)(rating));
                var lowLevel = Math.Floor((decimal)(rating));

                float collegeAdjustment;
                float nationalAdjustment;

                if (p.Gender.Equals("m", StringComparison.OrdinalIgnoreCase))
                {
                    // to correct for the 16 - 16.5 range case
                    var adj = lowLevel >= 16 ? 15.75f : Convert.ToInt32(lowLevel);

                    var fw = lowLevel == 0 ? 0 : curve.CollegeCorrectionsMale[lowLevel.ToString()];
                    var cw = highLevel == 0 ? 0 : curve.CollegeCorrectionsMale[highLevel.ToString()];

                    collegeAdjustment = fw + (((float)(rating) - adj) * (cw - fw));

                    var fw2 = lowLevel == 0 ? 0 : curve.NonCollegeCorrectionsMale[lowLevel.ToString()];
                    var cw2 = highLevel == 0 ? 0 : curve.NonCollegeCorrectionsMale[highLevel.ToString()];
                    nationalAdjustment = fw2 + (((float)(rating) - adj) * (cw2 - fw2));
                }
                else
                {
                    var adj = lowLevel >= 13 ? 12.75f : Convert.ToInt32(lowLevel);

                    var fw = lowLevel == 0 ? 0 : curve.CollegeCorrectionsFemale[lowLevel.ToString()];
                    var cw = highLevel == 0 ? 0 : curve.CollegeCorrectionsFemale[highLevel.ToString()];
                    collegeAdjustment = fw + (((float)(rating) - adj) * (cw - fw));

                    var fw2 = lowLevel == 0 ? 0 : curve.NonCollegeCorrectionsFemale[lowLevel.ToString()];
                    var cw2 = highLevel == 0 ? 0 : curve.NonCollegeCorrectionsFemale[highLevel.ToString()];
                    nationalAdjustment = fw2 + (((float)(rating) - adj) * (cw2 - fw2));
                }

                var correction = collegeAdjustment + ((1 - percentAgainstCollege) * (nationalAdjustment - collegeAdjustment));
                var finalrating = SmoothRating(p.Gender, reliability, Convert.ToDouble(rating + correction));

                p.Stats.FinalRating = Math.Truncate(finalrating * 100) / 100;

                // correct sub ratings        
                if (p.Stats.SubRating != null)
                {
                    // assuming all double props are ratings
                    foreach (PropertyInfo prop in p.Stats.SubRating.GetType().GetProperties())
                    {
                        var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                        if (type == typeof(double))
                        {
                            var propValue = prop.GetValue(p.Stats.SubRating, null);
                            if (propValue != null)
                            {
                                prop.SetValue(p.Stats.SubRating,
                                    SmoothRating(p.Gender, reliability, (double)propValue + correction));
                            }
                        }
                    }
                }
            }
        }

        private static float GetPercentCollegeNationality(List<Result> allResults, RatingRule rule)
        {
            var importIds = new[] { 6, 8, 14, 16, 36, 37, 24, 7};  // TODO: hardcoded import types
            var importSubTypes = new[] { "LTATour", "AustraliaAMT" };
            var total = 0;
            var totalCollege = 0;

            if (allResults == null) // skip if player has no  results
            {
                return 0;
            }
            allResults = new List<Result>(allResults.OrderByDescending(r => r.ResultDate));
            var previousResult = new Result();
            foreach (Result result in allResults)
            {
                if (result.ResultDate > rule.ResultThreshold) // Last 30 or Last Year (variable based on rating rules)
                {
                    int compare = DateTime.Compare(result.ResultDate, previousResult.ResultDate);
                    if ((total < rule.NumberOfResults) || (compare == 0))
                    {
                        total++;
                        if (result.DataImportTypeId != null)
                        {
                            if (importIds.Contains((int)result.DataImportTypeId) || importSubTypes.Contains(result.DataImportSubType))
                            {
                                totalCollege++;
                            }
                        }
                    }
                }
            }
            return total <= 0 ? 0 : (float)totalCollege / total;
        }

        private void UpdateDisconnectedPools(RatingRule rule, IDictionary<int, List<Result>> results)
        {
            _status = "Checking for disconnected pools...";
            var graph = SanityCheck.FindGroups(results, rule.disconnectedPoolThreshold);
            var disconnectedPlayerIds = new List<int>();
            foreach (var pool in graph)
            {
                disconnectedPlayerIds.AddRange(pool.Nodes);
            }
            using (var connection = new SqlConnection(_config.ConnectionStrings.DefaultConnection))
            {
                const string rel = "RatingReliability";
                const string rat = "ActualRating";
                // break update list into batches of 2000 since mssql has a param cap of 2100
                for (var i = 0; i < (float)disconnectedPlayerIds.Count / 2000; i++)
                {
                    var sub = disconnectedPlayerIds.Skip(i * 2000).Take(2000);
                    connection.Execute(
                        @"Update PlayerRating Set FinalRating = 0, " + rat + " = 0, " + rel +
                        " = 0 WHERE playerId in @Disconnected", new { Disconnected = sub }, commandTimeout: 300);
                }
            }
        }
    }
}
