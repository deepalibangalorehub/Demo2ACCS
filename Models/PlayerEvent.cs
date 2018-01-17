using System;
using System.ComponentModel.DataAnnotations.Schema;
using UniversalTennis.DataObjects.Json;

namespace UniversalTennis.Algorithm.Models
{
    public enum PlayerEventType
    {
        Created,
        Deleted,
        Merged,
        Updated
    }

    public class PlayerEvent
    {
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public PlayerEventType Type { get; set; }
        public DateTime DateCreated { get; set; }
        [NotMapped]
        public PlayerEventDoc Info { get; set; }
        public string InfoDoc { get; set; }
    }
}
