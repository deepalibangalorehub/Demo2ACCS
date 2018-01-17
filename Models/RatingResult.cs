namespace UniversalTennis.Algorithm.Models
{
    public class RatingResult
    {
        public int Id { get; set; }
        public int ResultId { get; set; }
        public double? Winner1CalculatedRating { get; set; }
        public double? Winner2CalculatedRating { get; set; }
        public double? Loser1CalculatedRating { get; set; }
        public double? Loser2CalculatedRating { get; set; }
        public double? Winner1Rating { get; set; }
        public double? Winner2Rating { get; set; }
        public double? Loser1Rating { get; set; }
        public double? Loser2Rating { get; set; }
        public double? Winner1Reliability { get; set; }
        public double? Winner2Reliability { get; set; }
        public double? Loser1Reliability { get; set; }
        public double? Loser2Reliability { get; set; }
    }
}
