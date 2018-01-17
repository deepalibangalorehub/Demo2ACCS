using System;

namespace UniversalTennis.Algorithm.Models
{
    public class RatingJob
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public string Type { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; }
    }
}
