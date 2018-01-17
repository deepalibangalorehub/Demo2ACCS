using System;

namespace UniversalTennis.Algorithm.Models
{
    public class DailyRating
    {
        public int Id { get; set; }
        public int PlayerRatingId { get; set; }
        public double Rating { get; set; }
        public double Reliability { get; set; }
        public string Algorithm { get; set; }
        public DateTime Date { get; set; }
        public virtual PlayerRating PlayerRating { get; set; }
    }
}
