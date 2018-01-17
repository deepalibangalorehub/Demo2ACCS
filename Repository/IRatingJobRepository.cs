using System.Threading.Tasks;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Repository
{
    public interface IRatingJobRepository
    {
        Task<RatingJob> GetById(int id);
        Task<RatingJob> GetActive(string type = null);
        Task<RatingJob> GetLast(string type = null);
        Task UpdateStatusById(int id, string status);
        Task<RatingJob> Insert(RatingJob job);
        Task Update(RatingJob job);
    }
}
