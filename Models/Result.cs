using System;
using Newtonsoft.Json;

namespace UniversalTennis.Algorithm.Models
{
    public enum SurfaceType
    {
        Hard,
        Clay,
        Grass,
        Synthetic,
        Other,
        Unknown
    }

    public class Result
    {
        public int ThresholdTwelve = 7;
        public int ThresholdEighteen = 12;
        public int ThresholdEight = 5;
        public int ThresholdSix = 4;

        public int RoutineThresholdTwelve = 5;
        public int RoutineThresholdEighteen = 7;
        public int RoutineThresholdEight = 3;
        public int RoutineThresholdSix = 3;        

        public enum MatchFormat { MiniSet, EightGameProSet, BestOfThreeSets, OneSet, BestOfFiveSets }
        public enum MatchGender { Male, Female, Coed }

        // winner gets credit for 2 games if 3rd set is tiebreaker i.e.  6-3, 4-6, 1-0
        public int WinnerGameCount => (WinnerSet1 ?? 0) + (WinnerSet2 ?? 0) + (WinnerSet3 == 1 ? 2 : (WinnerSet3 ?? 0)) + (WinnerSet4 ?? 0) + (WinnerSet5 ?? 0);
        public int LoserGameCount => (LoserSet1 ?? 0) + (LoserSet2 ?? 0) + (LoserSet3 ?? 0) + (LoserSet4 ?? 0) + (LoserSet5 ?? 0);

        // TODO: need to join type
        public bool IsCollegeMatch => DataImportTypeId == 6;

        public double PercentOfGamesWon(int playerId)
        {
            if (playerId == Winner1Id || playerId == Winner2Id)
                return WinnerGameCount / ((double)WinnerGameCount + LoserGameCount);
            if (playerId == Loser1Id || playerId == Loser2Id)
                return LoserGameCount / ((double)WinnerGameCount + LoserGameCount);
            throw new RatingException("Player does not match any participant in this match");
        }

        public bool ScoreIsValid()
        {
            // either player won at least 4 games
            if (WinnerSet1 < 4 && LoserSet1 < 4)
                return false;
            if (WinnerSet2 < 4 && LoserSet2 < 4 && WinnerSet2 > 0 &&
                LoserSet2 > 0 && WinnerGameCount < 4 && LoserGameCount < 4)
                return false;
            return true;
        }

        public MatchFormat MatchType
        {
            get
            {
                if (WinnerSet1 > 7)
                {
                    return MatchFormat.EightGameProSet;
                }
                if (WinnerSet1 < 6 && LoserSet1 < 6) //Does not hold true for DNF
                {
                    return MatchFormat.MiniSet;
                }
                if ((WinnerSet2 <= 0 || WinnerSet2 == null) && (LoserSet2 <= 0 || LoserSet2 == null))
                {
                    return MatchFormat.OneSet;
                }
                if ((WinnerSet4 > 0 || LoserSet4 > 0) || (WinnerSet1 > LoserSet1 && WinnerSet2 > LoserSet2 && WinnerSet3 > LoserSet3))
                {
                    return MatchFormat.BestOfFiveSets;
                }
                return MatchFormat.BestOfThreeSets;
            }
        }

        public bool IsCompetitive()
        {
            var loserGameCount = this.LoserGameCount;

            if ((MatchType == MatchFormat.MiniSet || MatchType == MatchFormat.EightGameProSet) && loserGameCount >= ThresholdEight)
            {
                return true;
            }
            if (MatchType == MatchFormat.BestOfThreeSets && loserGameCount >= ThresholdTwelve) //Or game count difference is 1  
            {
                return true;
            }
            if (MatchType == MatchFormat.OneSet && loserGameCount >= ThresholdSix)
            {
                return true;
            }
            if (MatchType == MatchFormat.BestOfFiveSets && loserGameCount >= ThresholdEighteen)
            {
                return true;
            }
            return false;
        }

        public int[] WinnerSets => new[] { WinnerSet1 ?? 0, WinnerSet2 ?? 0, WinnerSet3 ?? 0, WinnerSet4 ?? 0, WinnerSet5 ?? 0 };
        public int[] LoserSets => new[] { LoserSet1 ?? 0, LoserSet2 ?? 0, LoserSet3 ?? 0, LoserSet4 ?? 0, LoserSet5 ?? 0 };
        //public int[] TiebreakSets => new[] { TiebreakerSet1, TiebreakerSet2, TiebreakerSet3, TiebreakerSet4, TiebreakerSet5 };

        public bool IsDNF
        {
            //TODO, determine if it is a DNF match
            get
            {
                return false;
            }
        }

        public int CompletedSets
        {
            //TODO, determine how to count completed sets
            get
            {
                return 0;
            }
        }

        public string ScoreHtml
        {
            get
            {
                int[] winnerSet = WinnerSets;
                int[] loserSet = LoserSets;
                //int[] tiebreakSet = TiebreakSets;

                string score = "";
                for (int i = 0; i < 5; i++)
                {
                    if (winnerSet[i] != 0 || loserSet[i] != 0)
                    {
                        score += "<span>";
                        score += winnerSet[i] + "-" + loserSet[i] + " ";
                        // TODO: api doesn't provide tiebreakers yet
                        //score += (tiebreakSet[i] == 0 || tiebreakSet[i] == -1) ? " " : "(" + tiebreakSet[i] + ") ";
                        score += "</span>";
                    }
                }
                return score;
            }
        }

        public int Id { get; set; }
        public int Winner1Id { get; set; }
        public int? Winner2Id { get; set; }
        public string TeamType { get; set; }

        public int? WinnerSet1 => Score[0][0];
        public int? WinnerSet2 => Score[1][0];
        public int? WinnerSet3 => Score[2][0];
        public int? WinnerSet4 => Score[3][0];
        public int? WinnerSet5 => Score[4][0];
        public int? LoserSet1 => Score[0][1];
        public int? LoserSet2 => Score[1][1];
        public int? LoserSet3 => Score[2][1];
        public int? LoserSet4 => Score[3][1];
        public int? LoserSet5 => Score[4][1];

        /*
        public int TiebreakerSet1 { get; set; }
        public int TiebreakerSet2 { get; set; }
        public int TiebreakerSet3 { get; set; }
        public int TiebreakerSet4 { get; set; }
        public int TiebreakerSet5 { get; set; }
        */

        public int Loser1Id { get; set; }
        public int? Loser2Id { get; set; }
        [JsonProperty("date")]
        public DateTime ResultDate { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateLastUpdated { get; set; }
        public int? DrawId { get; set; }
        public string Outcome { get; set; }
        public string Competitiveness { get; set; }
        public int? DataImportTypeId { get; set; }
        public string DataImportSubType { get; set; }
        public bool? IsMastersOrGrandslam { get; set; }
        public SurfaceType? SurfaceType { get; set; }

        public int?[][] Score { get; set; }
    }
}
