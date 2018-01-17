namespace UniversalTennis.Algorithm.Models
{
    public class RatingDistributionSingles
    {
        public double? ActualRating { get; set; }
        public double? FinalRating { get; set; }
        public double? RatingReliability { get; set; }
    }

    public class RatingDistributionDoubles
    {
        public double? DoublesRating { get; set; }
        public double? FinalDoublesRating { get; set; }
        public double? DoublesReliability { get; set; }
    }
}
