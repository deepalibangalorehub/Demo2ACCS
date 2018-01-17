using System.Collections.Generic;

namespace UniversalTennis.Algorithm.Models
{
    public class DoublesRatingCalculation
    {
        public double Rating { get; set; }
        public double Reliability { get; set; }
        public List<DoublesResultCalculation> ResultCalculations { get; set; }
    }

    public class DoublesResultCalculation
    {
        public string WinLoss { get; set; }
        public string Score { get; set; }
        public string PartnerName { get; set; }
        public string Opponent1Name { get; set; }
        public string Opponent2Name { get; set; }
        public TeamInfo PlayerTeam { get; set; }
        public TeamInfo OpponentTeam { get; set; }
        public RatingInfoDoubles.WeightingFactors WeightingFactors { get; set; }
        public double DynamicRating { get; set; }
        public double TeamDynamicRating { get; set; }
    }

    public class SinglesRatingCalculation
    {
        public double Rating { get; set; }
        public double Reliability { get; set; }
        public List<SinglesResultCalculation> ResultCalculations { get; set; }
    }

    public class SinglesResultCalculation
    {
        public string WinLoss { get; set; }
        public string Score { get; set; }
        public string OpponentName { get; set; }
        public double OpponentReliability { get; set; }
        public double OpponentRating { get; set; }
        public RatingInfo.WeightingFactors WeightingFactors { get; set; }
        public double DynamicRating { get; set; }
    }
}
