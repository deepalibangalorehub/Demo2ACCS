
using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalTennis.Algorithm.Models
{
    public class Player
    {
        public int Id { get; set; }
        public string Gender { get; set; }
        public int? CountryId { get; set; }
        public int? CollegeId { get; set; }
        public string DisplayName { get; set; }
        public double? BenchmarkRating { get; set; }
        public List<ThirdPartyRanking> ThirdPartyRanking { get; set; }
        public PlayerRating Stats { get; set; }

        /* Is player a currently ranked top 500 atp or WTA singles player */
        public bool IsTop700()
        {
            if (ThirdPartyRanking != null && ThirdPartyRanking
                .Any(t => (t.Source == "ATP" || t.Source == "WTA") && t.Rank <= 700 && t.Type == "Singles"))
            {
                return true;
            }
            return false;
        }
    }

    public class ThirdPartyRanking
    {
        public string Source { get; set; }
        public string Type { get; set; }
        public int Rank { get; set; }
    }
}
