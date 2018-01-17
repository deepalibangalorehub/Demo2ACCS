using System.Threading.Tasks;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Repository
{
    public interface ISubRatingRepository
    {
        Task AddOrUpdateSubRating(SubRating subRating);
        SubRatingsWithRankings GetPlayerSubRatingsAndRankings(int playerId);
    }
}
