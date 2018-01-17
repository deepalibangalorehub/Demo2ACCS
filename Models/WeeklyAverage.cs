using System;

namespace UniversalTennis.Algorithm.Models
{
    public class WeeklyAverage
    {
        public int Id { get; set; }
        public int PlayerRatingId { get; set; }
        public double Rating { get; set; }
        public RatingStatus RatingStatus { get; set; }
        public string Type { get; set; }
        public DateTime Date { get; set; }
        public virtual PlayerRating PlayerRating { get; set; }
    }
}
