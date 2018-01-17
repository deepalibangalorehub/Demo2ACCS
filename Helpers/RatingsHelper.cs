using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Helpers
{
    public class RatingsHelper
    {
        public static RatingStatus RatingStatusFromString(string ratingStatus)
        {
            switch (ratingStatus?.ToLower())
            {
                case "rated":
                    return RatingStatus.Rated;
                case "projected":
                    return RatingStatus.Projected;
                case "inactive":
                    return RatingStatus.Inactive;
                case "unrated":
                    return RatingStatus.Unrated;
                default:
                    return RatingStatus.Invalid;
            }
        }
    }
}
