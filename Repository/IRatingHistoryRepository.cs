using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Repository
{
    public interface IRatingHistoryRepository
    {
        IEnumerable<WeeklyAverage> GetAllWeeklyByPlayerId(int playerId);
        IEnumerable<WeeklyAverage> GetAllWeeklyByPlayerId(int playerId, RatingStatus ratingStatus);
        WeeklyAverage GetWeeklyOnDateByPlayerId(int playerId, DateTime date);
        WeeklyAverage GetWeeklyOnDateByPlayerId(int playerId, DateTime date, RatingStatus ratingStatus);
        Task DeleteDailyRatingByPlayerId(int playerId);
        Task DeleteWeeklyAverageByPlayerId(int playerId);
        List<DailyRating> MergeDailyRatings(int[] sourcePlayerIds, int targetPlayerId);
        void MergeWeeklyAverage(int[] sourcePlayerIds, int targetPlayerRatingId, List<DailyRating> targetDailys);
        DailyRating GetDailyratingForPlayer(int playerId, DateTime date, string algorithmType);
    }
}