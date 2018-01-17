using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using Newtonsoft.Json;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm
{
    public class RatingRule
    {
        public V2NormalizationCurve NormalizationCurve;
        public DateTime ResultThreshold = DateTime.Now.AddMonths(-12); //Max Age of Result
        public int NumberOfResults = 30;

        public float MaxLevelDifference = 3.53f;
        public float MaxLevelDifferenceProfessional = 3.65f;
        public int MaxReliability = 10;
        public float ReliabilityWeight = 8;

        // These are currently statically defined in Result
        public int ThresholdTwelve = 7;
        public int ThresholdEighteen = 12;
        public int ThresholdEight = 5;
        public int ThresholdSix = 4;

        /*
        public int RoutineThresholdTwelve = 5;
        public int RoutineThresholdEighteen = 7;
        public int RoutineThresholdEight = 3;
        public int RoutineThresholdSix = 3;
        */

        //Steve Algorithm Additions 5-28-14
        public float MaxUTRDelta = 2.5f;
        public float NormalMatchMaxUTRDelta = 2.5f;
        public float CloseMatchMaxUTRDelta = 0.16f;
        public float BestOfFiveSetReliability = 1.0f;
        public float BestOfThreeSetReliability = 1.0f;
        public float EightGameProSetReliability = 0.7f;
        public float MiniSetReliability = 0.4f;
        public float OneSetReliability = 0;
        public float LopsidedMatchReliability = 0.25f;
        public float LopsidedGameRatio = 0.13f;
        public float UnderDogMatchReliability = 0;
        public float CompetitiveUnderDogMatchReliability = 0.25f;
        public float CompetitivenessFactorMultiplier = 0.3f;
        public float MinRatingRelibility = 0.1f;
        public float BenchmarkMatchCoeffecient = 0.1f;
        public float interpoolCoefficientCollege = 1.0f;
        public float interpoolCoefficientCountry = 1.0f;
        public int disconnectedPoolThreshold = 3;
        public bool EnableOpponentRatingReliability;
        public bool EnableMatchFormatReliability;
        public bool EnableMatchFrequencyReliability;
        public bool EnableMatchCompetitivenessCoeffecient;
        public bool EnableBenchmarkMatchCoeffecient;
        public bool EnableInterpoolCoeffecient;
        public float femaleUTRScaleGraduationHigh = 1.5f;
        public float femaleUTRScaleGraduationLow = 1.5f;
        public float maleUTRScaleGraduationHigh = 1.5f;
        public float maleUTRScaleGraduationLow = 1.5f;
        public float femaleUTRCollegeScaleGraduationHigh = 1.5f;
        public float femaleUTRCollegeScaleGraduationLow = 1.5f;
        public float maleUTRCollegeScaleGraduationHigh = 1.5f;
        public float maleUTRCollegeScaleGraduationLow = 1.5f;
        public float maleUTRCollegeScaleLossPercentage = 1f;
        public float femaleUTRCollegeScaleLossPercentage = 1f;
        public float maleScaleLossPercentageMaxLevel = 10f;
        public float femaleScaleLossPercentageMaxLevel = 10f;
        public float partnerSpreadAdjustmentFactor = 0.21f;
        public float minPartnerFrequencyFactor = 0.60f;
        public float singlesWeightOnDoubles = 0.60f;
        public float singlesWeightReliabilityThreshold = 0.1f;
        public float provisionalSinglesReliabilityThreshold = 0.1f;
        public float provisionalDoublesReliabilityThreshold = 0.1f;
        public float benchmarkTrailSpan = 0.5f;
        public float eligibleResultsWeightThreshold = 0.001f;
        public bool EnablePartnerFrequencyReliability = true;
        public bool EnableDynamicRatingCap;
        public bool EnableCompetitivenessFilter = false;

        public static RatingRule GetDefault(string type, string connStr)
        {
            RatingRule r = new RatingRule();

            /*
            r.ResultThreshold = DateTime.Now.AddMonths(-1 * Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["OldestResultInMonths"]));
            r.NumberOfResults = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["NumberOfResults"]);
            r.MaxReliability = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["MaxReliability"]);
            r.ReliabilityWeight = Convert.ToSingle(System.Configuration.ConfigurationManager.AppSettings["ReliabilityWeight"]);
            

            SqlConnection conn = new SqlConnection("Server=.\\SQLSERVER;Database=universaltennis_rating;Trusted_Connection=True;Connection Timeout=90");
            conn.Open();
            var magicNumbers = conn.Query<string>("select top 1 Doc from adminsettings where type = @param",
                new { param = "MagicNumbers" }).First();
            conn.Close();
            var magicNumberMap = JsonConvert.DeserializeObject<Dictionary<string, double>>(magicNumbers);
            r.MaxLevelDifference = (float)magicNumberMap["MaxLevelDifference"];
            r.MaxLevelDifferenceProfessional = (float)magicNumberMap["MaxLevelDifferenceProfessional"];
            */

            using (var conn = new SqlConnection(connStr))
            {
                string curveKey;           
                switch (type)
                {
                    case "singles":
                        r = SetSinglesVariables(r, conn);
                        curveKey = "V2NormalizationCurve";
                        break;
                    case "doubles":
                        r = SetDoublesVariables(r, conn);
                        curveKey = "DoublesNormalizationCurve";
                        break;
                    default:
                        r = SetSinglesVariables(r, conn);
                        curveKey = "V2NormalizationCurve";
                        break;
                }
                const string query = @"select Doc from AlgorithmSetting where type = @Type";
                var settings = conn.Query<AlgorithmSettings>(query, new {Type = curveKey}).SingleOrDefault();
                if (settings == null) throw new RatingException("Could not find curve settings");
                r.NormalizationCurve = JsonConvert.DeserializeObject<V2NormalizationCurve>(settings.Doc);
            }
            return r;
        }

        private static RatingRule SetSinglesVariables(RatingRule r, IDbConnection conn)
        {
            var settings = conn.Query<AlgorithmSettings>("select Doc from algorithmsetting where type = @Type",
                new {Type = "V2Variables"}).First();

            var variables = JsonConvert.DeserializeObject<V2Settings>(settings.Doc);

            r.MaxUTRDelta = variables.MaxUTRDelta;
            r.NormalMatchMaxUTRDelta = variables.NormalMatchMaxUTRDelta;
            r.CloseMatchMaxUTRDelta = variables.CloseMatchMaxUTRDelta;
            r.BestOfFiveSetReliability = variables.BestOfFiveSetReliability;
            r.BestOfThreeSetReliability = variables.BestOfThreeSetReliability;
            r.EightGameProSetReliability = variables.EightGameProSetReliability;
            r.MiniSetReliability = variables.MiniSetReliability;
            r.OneSetReliability = variables.OneSetReliability;
            r.LopsidedMatchReliability = variables.LopsidedMatchReliability;
            r.LopsidedGameRatio = variables.LopsidedGameRatio;
            r.UnderDogMatchReliability = variables.UnderDogMatchReliability;
            r.CompetitiveUnderDogMatchReliability = variables.CompetitiveUnderDogMatchReliability;
            r.CompetitivenessFactorMultiplier = variables.CompetitivenessFactorMultiplier;
            r.MinRatingRelibility = variables.MinRatingRelibility;
            r.BenchmarkMatchCoeffecient = variables.BenchmarkMatchCoeffecient;
            r.interpoolCoefficientCollege = variables.InterpoolCoefficientCollege;
            r.interpoolCoefficientCountry = variables.InterpoolCoefficientCountry;
            r.EnableOpponentRatingReliability = variables.EnableOpponentRatingReliability;
            r.EnableMatchFormatReliability = variables.EnableMatchFormatReliability;
            r.EnableMatchFrequencyReliability = variables.EnableMatchFrequencyReliability;
            r.EnableMatchCompetitivenessCoeffecient = variables.EnableMatchCompetitivenessCoeffecient;
            r.EnableBenchmarkMatchCoeffecient = variables.EnableBenchmarkMatchCoeffecient;
            r.EnableInterpoolCoeffecient = variables.EnableInterpoolCoeffecient;
            r.EnableDynamicRatingCap = variables.EnableDynamicRatingCap;
            r.maleUTRScaleGraduationHigh = variables.MaleUTRScaleGraduationHigh;
            r.maleUTRScaleGraduationLow = variables.MaleUTRScaleGraduationLow;
            r.femaleUTRScaleGraduationHigh = variables.FemaleUTRScaleGraduationHigh;
            r.femaleUTRScaleGraduationLow = variables.FemaleUTRScaleGraduationLow;
            r.maleUTRCollegeScaleGraduationHigh = variables.MaleUTRCollegeScaleGraduationHigh;
            r.maleUTRCollegeScaleGraduationLow = variables.MaleUTRCollegeScaleGraduationLow;
            r.femaleUTRCollegeScaleGraduationHigh = variables.FemaleUTRCollegeScaleGraduationHigh;
            r.femaleUTRCollegeScaleGraduationLow = variables.FemaleUTRCollegeScaleGraduationLow;
            r.maleUTRCollegeScaleLossPercentage = variables.MaleUTRCollegeScaleLossPercentage;
            r.femaleUTRCollegeScaleLossPercentage = variables.FemaleUTRCollegeScaleLossPercentage;
            r.maleScaleLossPercentageMaxLevel = variables.MaleScaleLossPercentageMaxLevel;
            r.femaleScaleLossPercentageMaxLevel = variables.FemaleScaleLossPercentageMaxLevel;
            r.disconnectedPoolThreshold = variables.DisconnectedPoolThreshold;
            r.eligibleResultsWeightThreshold = variables.EligibleResultWeightThreshold;
            return r;
        }

        private static RatingRule SetDoublesVariables(RatingRule r, IDbConnection conn)
        {
            var settings = conn.Query<AlgorithmSettings>("select Doc from algorithmsetting where type = @Type",
                new { Type = "DoublesVariables" }).First();

            var variables = JsonConvert.DeserializeObject<V2Settings>(settings.Doc);

            r.MaxUTRDelta = variables.MaxUTRDelta;
            r.NormalMatchMaxUTRDelta = variables.NormalMatchMaxUTRDelta;
            r.CloseMatchMaxUTRDelta = variables.CloseMatchMaxUTRDelta;
            r.BestOfFiveSetReliability = variables.BestOfFiveSetReliability;
            r.BestOfThreeSetReliability = variables.BestOfThreeSetReliability;
            r.EightGameProSetReliability = variables.EightGameProSetReliability;
            r.MiniSetReliability = variables.MiniSetReliability;
            r.OneSetReliability = variables.OneSetReliability;
            r.LopsidedMatchReliability = variables.LopsidedMatchReliability;
            r.LopsidedGameRatio = variables.LopsidedGameRatio;
            r.UnderDogMatchReliability = variables.UnderDogMatchReliability;
            r.CompetitiveUnderDogMatchReliability = variables.CompetitiveUnderDogMatchReliability;
            r.CompetitivenessFactorMultiplier = variables.CompetitivenessFactorMultiplier;
            r.MinRatingRelibility = variables.MinRatingRelibility;
            r.BenchmarkMatchCoeffecient = variables.BenchmarkMatchCoeffecient;
            r.maleUTRScaleGraduationHigh = variables.MaleUTRScaleGraduationHigh;
            r.maleUTRScaleGraduationLow = variables.MaleUTRScaleGraduationLow;
            r.interpoolCoefficientCollege = variables.InterpoolCoefficientCollege;
            r.interpoolCoefficientCountry = variables.InterpoolCoefficientCountry;
            r.EnableOpponentRatingReliability = variables.EnableOpponentRatingReliability;
            r.EnableMatchFormatReliability = variables.EnableMatchFormatReliability;
            r.EnableMatchFrequencyReliability = variables.EnableMatchFrequencyReliability;
            r.EnableMatchCompetitivenessCoeffecient = variables.EnableMatchCompetitivenessCoeffecient;
            r.EnableBenchmarkMatchCoeffecient = variables.EnableBenchmarkMatchCoeffecient;
            r.EnableInterpoolCoeffecient = variables.EnableInterpoolCoeffecient;
            r.EnableDynamicRatingCap = variables.EnableDynamicRatingCap;
            r.femaleUTRScaleGraduationHigh = variables.FemaleUTRScaleGraduationHigh;
            r.femaleUTRScaleGraduationLow = variables.FemaleUTRScaleGraduationLow;
            r.minPartnerFrequencyFactor = variables.MinPartnerFrequencyFactor;
            r.partnerSpreadAdjustmentFactor = variables.PartnerSpreadAdjustmentfactor;
            r.EnablePartnerFrequencyReliability = variables.EnablePartnerFrequencyReliability;
            r.singlesWeightOnDoubles = variables.SinglesWeightOnDoubles;
            r.singlesWeightReliabilityThreshold = variables.SinglesWeightReliabilityThreshold;
            r.provisionalSinglesReliabilityThreshold = variables.ProvisionalSinglesReliabilityThreshold;
            r.provisionalDoublesReliabilityThreshold = variables.ProvisionalDoublesReliabilityThreshold;
            r.disconnectedPoolThreshold = variables.DisconnectedPoolThreshold;
            r.EnableCompetitivenessFilter = variables.EnableCompetitivenessFilter;
            r.benchmarkTrailSpan = variables.BenchmarkTrailSpan;
            r.eligibleResultsWeightThreshold = variables.EligibleResultWeightThreshold;          
            return r;
        }

        public class V2Settings
        {
            public float MaxUTRDelta { get; set; }
            public float NormalMatchMaxUTRDelta { get; set; }
            public float CloseMatchMaxUTRDelta { get; set; }
            public float BestOfFiveSetReliability { get; set; }
            public float BestOfThreeSetReliability { get; set; }
            public float EightGameProSetReliability { get; set; }
            public float MiniSetReliability { get; set; }
            public float OneSetReliability { get; set; }
            public float LopsidedMatchReliability { get; set; }
            public float LopsidedGameRatio { get; set; }
            public float UnderDogMatchReliability { get; set; }
            public float CompetitiveUnderDogMatchReliability { get; set; }
            public float CompetitivenessFactorMultiplier { get; set; }
            public float MinRatingRelibility { get; set; }
            public float BenchmarkMatchCoeffecient { get; set; }
            public float InterpoolCoefficientCollege { get; set; }
            public float InterpoolCoefficientCountry { get; set; }
            public int DisconnectedPoolThreshold { get; set; }
            public bool EnableOpponentRatingReliability { get; set; }
            public bool EnableMatchFormatReliability { get; set; }
            public bool EnableMatchFrequencyReliability { get; set; }
            public bool EnableMatchCompetitivenessCoeffecient { get; set; }
            public bool EnableBenchmarkMatchCoeffecient { get; set; }
            public bool EnableInterpoolCoeffecient { get; set; }
            public bool EnablePartnerFrequencyReliability { get; set; }
            public bool EnableDynamicRatingCap { get; set; }
            public bool EnableCompetitivenessFilter { get; set; }
            public float FemaleUTRScaleGraduationHigh { get; set; }
            public float FemaleUTRScaleGraduationLow { get; set; }
            public float MaleUTRScaleGraduationHigh { get; set; }
            public float MaleUTRScaleGraduationLow { get; set; }
            public float FemaleUTRCollegeScaleGraduationHigh { get; set; }
            public float FemaleUTRCollegeScaleGraduationLow { get; set; }
            public float MaleUTRCollegeScaleGraduationHigh { get; set; }
            public float MaleUTRCollegeScaleGraduationLow { get; set; }
            public float MaleUTRCollegeScaleLossPercentage { get; set; }
            public float FemaleUTRCollegeScaleLossPercentage { get; set; }
            public float FemaleScaleLossPercentageMaxLevel { get; set; }
            public float MaleScaleLossPercentageMaxLevel { get; set; }
            public float PoolNormalizationMale { get; set; }
            public float PoolNormalizationFemale { get; set; }
            public float PartnerSpreadAdjustmentfactor { get; set; }
            public float MinPartnerFrequencyFactor { get; set; }
            public float SinglesWeightOnDoubles { get; set; }
            public float BenchmarkTrailSpan { get; set; }
            public float SinglesWeightReliabilityThreshold { get; set; }
            public float ProvisionalSinglesReliabilityThreshold { get; set; }
            public float ProvisionalDoublesReliabilityThreshold { get; set; }
            public float EligibleResultWeightThreshold { get; set; }
        }
    }
}
