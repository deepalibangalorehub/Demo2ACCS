namespace UniversalTennis.Algorithm
{
    public class RatingInfoDoubles
    {
        public WeightingFactors weightingFactors;

        public double Rating { get; set; }

        public double Reliability { get; set; }
        public bool AgainstBenchmark { get; set; }

        public struct WeightingFactors
        {
            public double OpponentRatingReliability { get; set; }

            public double MatchFormatReliability { get; set; }

            public double MatchFrequencyReliability { get; set; }

            public double MatchCompetitivenessCoeffecient { get; set; }

            public double BenchmarkMatchCoeffecient { get; set; }

            public double InterpoolCoeffecient { get; set; }

            public double MatchWeight { get; set; }
        }
    }
}
