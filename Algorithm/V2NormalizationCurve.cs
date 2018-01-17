using System.Collections.Generic;

namespace UniversalTennis.Algorithm
{
    public class V2NormalizationCurve
    {
        public Dictionary<string, float> CollegeCorrectionsMale { get; set; }
        public Dictionary<string, float> NonCollegeCorrectionsMale { get; set; }
        public Dictionary<string, float> CollegeCorrectionsFemale { get; set; }
        public Dictionary<string, float> NonCollegeCorrectionsFemale { get; set; }
    }
}
