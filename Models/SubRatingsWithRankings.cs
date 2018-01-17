using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UniversalTennis.Algorithm.Models
{
    public class SubRatingsWithRankings
    {
        public int Id { get; set; }
        public double? UTR { get; set; }
        public double? HardCourt { get; set; }
        public double? ClayCourt { get; set; }
        public double? GrassCourt { get; set; }
        public double? OneMonth { get; set; }
        public double? SixWeek { get; set; }
        public double? EightWeek { get; set; }
        public double? ThreeMonth { get; set; }
        public double? GrandSlamMasters { get; set; }
        public int? HardCourtCount { get; set; }
        public int? ClayCourtCount { get; set; }
        public int? GrassCourtCount { get; set; }
        public int? OneMonthCount { get; set; }
        public int? SixWeekCount { get; set; }
        public int? EightWeekCount { get; set; }
        public int? ThreeMonthCount { get; set; }
        public int? GrandSlamMastersCount { get; set; }
        public int? ResultCount { get; set; }
        public long? UtrRank { get; set; }
        public long? HardCourtRank { get; set; }
        public long? ClayCourtRank { get; set; }
        public long? GrassCourtRank { get; set; }
        public long? OneMonthRank { get; set; }
        public long? SixWeekRank { get; set; }
        public long? EightWeekRank { get; set; }
        public long? ThreeMonthRank { get; set; }
        public long? GrandSlamMastersRank { get; set; }
    }
}
