using System;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm
{
    public class RatingInfo
    {
        public WeightingFactors weightingFactors;
        public float Rating { get; set; }
        public SurfaceType? SurfaceType { get; set; }
        public bool? IsMastersOrGrandslam { get; set; }
        public SubRating SubRating { get; set; }
        public DateTime Date { get; set; }
        public float Reliability;
        public bool AgainstBenchmark { get; set; }

        public struct WeightingFactors
        {
            public float OpponentRatingReliability { get; set; }

            public float MatchFormatReliability { get; set; }

            public float MatchFrequencyReliability { get; set; }

            public float MatchCompetitivenessCoeffecient { get; set; }

            public float BenchmarkMatchCoeffecient { get; set; }

            public float InterpoolCoeffecient { get; set; }

            public float SurfaceWeight { get; set; }

            public float MatchWeight { get; set; }
        }
    }
}
