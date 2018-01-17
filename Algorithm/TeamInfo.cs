using System;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm {

    public class TeamInfo
    {
        public TeamInfo(Player player1, Player player2, bool isOpponent)
        {
            Player1Id = player1.Id;
            Player2Id = player2.Id;
            Player1CollegeId = player1.CollegeId ?? 0;
            Player2CollegeId = player2.CollegeId ?? 0;
            Player1CountryId = player1.CountryId ?? 0;
            Player2CountryId = player2.CountryId ?? 0;
            Player1Rating = player1.Stats.AssignedRating ?? 0;
            Player2Rating = player2.Stats.AssignedRating ?? 0;
            Player1Gender = player1.Gender;
            Player2Gender = player2.Gender;
            // use benchmark rating if available
            if (player1.Stats.DoublesBenchmarkRating > 0 && player1.Stats.DoublesBenchmarkRating != null && isOpponent)
                Player1Rating = player1.Stats.DoublesBenchmarkRating ?? 0;
            if (player2.Stats.DoublesBenchmarkRating > 0 && player2.Stats.DoublesBenchmarkRating != null && isOpponent)
                Player2Rating = player2.Stats.DoublesBenchmarkRating ?? 0;
            RatingDiff = Math.Abs(Player1Rating - Player2Rating);
            TeamRating = Math.Truncate(((Player1Rating + Player2Rating) / 2) * 100) / 100;
            TeamReliability = ((player1.Stats.AssignedReliability) + (player2.Stats.AssignedReliability)) / 2;
            HasCollegePlayer = (player1.CollegeId ?? 0) > 0 || (player2.CollegeId ?? 0) > 0;
        }

        public double RatingDiff { get; set; }
        public bool HasCollegePlayer { get; set; }
        public int Player1Id { get; set; }
        public int Player2Id { get; set; }
        public string Player1Gender { get; set; }
        public string Player2Gender { get; set; }
        public int Player1CollegeId { get; set; }
        public int Player1CountryId { get; set; }
        public int Player2CollegeId { get; set; }
        public int Player2CountryId { get; set; }
        public double Player1Rating { get; set; }
        public double Player2Rating { get; set; }
        public double TeamRating { get; set; }
        public double TeamReliability { get; set; }
    }
}
