using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniversalTennis.Algorithm.Models
{
    public enum RatingStatus
    {
        Invalid = -1,
        Unrated = 0,
        Inactive = 1,
        Projected = 2,
        Rated = 3
    }

    public class PlayerRating
    {
        private double? _rating;
        private double? _reliability;

        // temp values for doubles rating
        [NotMapped]
        public double? CalculatedRating { get; set; }
        [NotMapped]
        public double? CalculatedReliability { get; set; }
        [NotMapped]
        public double? AssignedRating { get; set; }
        [NotMapped]
        public double AssignedReliability { get; set; }

        // the current ratings
        [NotMapped]
        public double Rating
        {
            get => _rating == null ? ActualRating ?? 0 : _rating ?? 0;
            set => _rating = value;
        }

        [NotMapped]
        public double Reliability
        {
            get => _reliability == null ? RatingReliability ?? 0 : _reliability ?? 0;
            set => _reliability = value;
        }

        public int Id { get; set; }
        public int PlayerId { get; set; }
        public bool IsBenchmark { get; set; }
        public double? ActualRating { get; set; }
        public double? BenchmarkRating { get; set; }
        public double? CompetitiveMatchPct { get; set; }
        public double? CompetitiveMatchPctDoubles { get; set; }
        public double? DecisiveMatchPct { get; set; }
        public double? RoutineMatchPct { get; set; }
        public double? RatingReliability { get; set; }
        public double? PreviousRating { get; set; }
        public double? PreviousRatingReliability { get; set; }
        public double? AlternativeRating { get; set; }
        public double? AlternativeRatingReliability { get; set; }
        public double? InactiveRating { get; set; }
        public double? DoublesRating { get; set; }
        public double? DoublesReliability { get; set; }
        public double? FinalRating { get; set; }
        public double? FinalDoublesRating { get; set; }
        public double? DoublesBenchmarkRating { get; set; }
        public string ActiveSinglesResults { get; set; }
        public string ActiveDoublesResults { get; set; }
        public string PlayerGender { get; set; }

        public virtual ICollection<DailyRating> DailyRatings { get; set; }
        public virtual ICollection<WeeklyAverage> WeeklyAverages { get; set; }
        public virtual SubRating SubRating { get; set; }
    }
}
