using System;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm
{
    public class MatchCompetivenessCalculator
    {
        public static float CalculateMatchCompetivenessCoeffecient(Player player, Player opponent, Result matchInfo, RatingRule rule)
        {
            float UTRDelta = (float)Math.Abs(player.Stats.Rating - opponent.Stats.Rating), coeffecient = 1;

            if (UTRDelta <= rule.NormalMatchMaxUTRDelta && UTRDelta >= rule.CloseMatchMaxUTRDelta) //Normal match
            {
                coeffecient = DoCoeffcientCalculation(UTRDelta, rule);
            }
            else if (UTRDelta < rule.CloseMatchMaxUTRDelta) // Close Match
            {
                coeffecient = IsMatchLopsided(matchInfo, rule) ? rule.LopsidedMatchReliability : DoCoeffcientCalculation(UTRDelta, rule); //If a close match is lopsided, we assume one player must have been having an off game, reduced credit
            }
            else if (UTRDelta > rule.NormalMatchMaxUTRDelta) //Lopsided Match
            {
                coeffecient = CalculateUnderdogMatch(player, opponent, matchInfo, rule); //Underdog matches, where player's have a large skill gap, calculate differently
            }
            else
            {
                throw new RatingException("Unable to determine competitiveness coeficcient for result id: " +  matchInfo.Id + ", " + matchInfo.ScoreHtml + ", " + UTRDelta);
                //coeffecient = 0.0f; //Fail safe, every match should be caught above, may want to throw exception here
            }
            return coeffecient;
        }

        
        // same thing but with team ratings
        public static float CalculateMatchCompetivenessCoeffecient(TeamInfo playerTeam, TeamInfo opponentTeam, Result matchInfo, RatingRule rule)
        {
            float UTRDelta = (float)Math.Abs(playerTeam.TeamRating - opponentTeam.TeamRating), coeffecient = 1;

            if (UTRDelta <= rule.NormalMatchMaxUTRDelta && UTRDelta >= rule.CloseMatchMaxUTRDelta) //Normal match
            {
                coeffecient = DoCoeffcientCalculation(UTRDelta, rule);
            }
            else if (UTRDelta < rule.CloseMatchMaxUTRDelta) // Close Match
            {
                coeffecient = IsMatchLopsided(matchInfo, rule) ? rule.LopsidedMatchReliability : DoCoeffcientCalculation(UTRDelta, rule); //If a close match is lopsided, we assume one player must have been having an off game, reduced credit
            }
            else if (UTRDelta > rule.NormalMatchMaxUTRDelta) //Lopsided Match
            {
                coeffecient = CalculateUnderdogMatch(playerTeam, opponentTeam, matchInfo, rule); //Underdog matches, where player's have a large skill gap, calculate differently
            }
            else
            {
                throw new RatingException("Unable to determine competitiveness coeficcient for result id: " + matchInfo.Id);
                //coeffecient = 0.0f; //Fail safe, every match should be caught above, may want to throw exception here
            }
            return coeffecient;
        }
        

        public static float DoCoeffcientCalculation(float delta, RatingRule rule)
        {
            return 1 - (rule.CompetitivenessFactorMultiplier * delta);
        }

        public static bool IsMatchLopsided(Result matchInfo, RatingRule rule)
        {
            float matchRatio = matchInfo.LoserGameCount / ((float)matchInfo.WinnerGameCount + matchInfo.LoserGameCount);
            return (matchRatio <= rule.LopsidedGameRatio);
        }

        public static float CalculateUnderdogMatch(Player player, Player opponent, Result matchInfo, RatingRule rule)
        {
            Player winner, loser;
            if (matchInfo.Winner1Id == player.Id)
            {
                winner = player;
                loser = opponent;
            }
            else
            {
                winner = opponent;
                loser = player;
            }
            if (winner.Stats.Rating > loser.Stats.Rating && !CompetiveThresholdReached(matchInfo)) //If the expected player won and the loser didn't reach competitive threshold, it's expected.
            {
                return rule.UnderDogMatchReliability;
            }
            else //Underdog was competitive
            {
                return rule.CompetitiveUnderDogMatchReliability;
            }
        }

        
        // same thing but with team ratings
        public static float CalculateUnderdogMatch(TeamInfo playerTeam, TeamInfo opponentTeam, Result matchInfo, RatingRule rule)
        {
            TeamInfo winner, loser;
            if (matchInfo.Winner1Id == playerTeam.Player1Id || matchInfo.Winner1Id == playerTeam.Player2Id)
            {
                winner = playerTeam;
                loser = opponentTeam;
            }
            else
            {
                winner = opponentTeam;
                loser = playerTeam;
            }
            if (winner.TeamRating > loser.TeamRating && !CompetiveThresholdReached(matchInfo)) //If the expected team won and the loser didn't reach competitive threshold, it's expected.
            {
                return rule.UnderDogMatchReliability;
            }
            else //Underdog was competitive
            {
                return rule.CompetitiveUnderDogMatchReliability;
            }
        }
        

        public static bool CompetiveThresholdReached(Result matchInfo)
        {
            return matchInfo.IsCompetitive();
        }
    }
}
