using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniversalTennis.Algorithm.Models
{
    public class SubRating
    {
        public int Id { get; set; }
        public int PlayerRatingId { get; set; }
        public int? ResultCount { get; set; }
        public double? OneMonth { get; set; }
        public int? OneMonthCount { get; set; }
        public double? ThreeMonth { get; set; }
        public int? ThreeMonthCount { get; set; }
        public double? SixWeek { get; set; }
        public int? SixWeekCount { get; set; }
        public double? EightWeek { get; set; }
        public int? EightWeekCount { get; set; }
        public double? HardCourt { get; set; }
        public int? HardCourtCount { get; set; }
        public double? ClayCourt { get; set; }
        public int? ClayCourtCount { get; set; }
        public double? GrassCourt { get; set; }
        public int? GrassCourtCount { get; set; }
        public double? GrandSlamMasters { get; set; }
        public int? GrandSlamMastersCount { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateLastUpdated { get; set; }

        public virtual PlayerRating PlayerRating { get; set; }
    }
}
