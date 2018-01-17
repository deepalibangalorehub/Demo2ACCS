using System.Threading.Tasks;

namespace UniversalTennis.Algorithm.Service
{
    public interface IJobService
    {
        Task UpdateRatingsAndResults();
        Task<bool> AlgorithmIsRunning();
    }
}
