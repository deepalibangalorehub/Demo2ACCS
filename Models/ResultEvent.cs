using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniversalTennis.Algorithm.Models
{
    public enum ResultEventType
    {
        Created,
        Deleted,
        Updated
    }

    public class ResultEvent
    {
        public int Id { get; set; }
        public int ResultId { get; set; }
        [NotMapped]
        public ResultEventInfo Info { get; set; }
        public string InfoDoc { get; set; }
        public ResultEventType Type { get; set; }
        public DateTime DateCreated { get; set; }
    }

    public class ResultEventInfo
    {
        public int Winner1Id { get; set; }
        public int? Winner2Id { get; set; }
        public int Loser1Id { get; set; }
        public int? Loser2Id { get; set; }
    }
}
