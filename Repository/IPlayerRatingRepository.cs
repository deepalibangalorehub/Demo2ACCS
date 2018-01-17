using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Repository
{
    public interface IPlayerRatingRepository
    {
        Task<PlayerRating> GetById(int id);
        Task<PlayerRating> GetByPlayerId(int playerId);
        Task<List<PlayerRating>> GetByPlayerIds(int[] playerIds);
        Task<Dictionary<int, PlayerRating>> GetAll();
        Task<List<PlayerRating>> GetInactive();
        int Count(int level, Expression<Func<PlayerRating, bool>> query);
        List<RatingDistributionSingles> GetForSinglesDistribution();
        List<RatingDistributionDoubles> GetForDoublesDistribution();
        Task<bool> Exists(Expression<Func<PlayerRating, bool>> query);
        Task<PlayerRating> Insert(PlayerRating playerRating);
        Task InsertList(List<PlayerRating> playerRatings);
        Task Delete(PlayerRating playerRating);
        Task DeleteList(List<PlayerRating> playerRatings);
    }
}
