using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Service
{
    public interface IPlayerService
    {
        Task<int[]> GetEligibleSinglesResults(int playerId);
        Task<int[]> GetEligibleDoublesResults(int playerId);
        Task<Player> GetPlayerInfo(int playerId);
        Task<List<Player>> GetPlayerInfo(int[] playerIds);
        Task<List<Player>> GetPlayersWithResults(string type, DateTime thresholdDate);
        Task<List<Player>> LoadPlayerRatingsForAlgorithm(List<Player> players, string type);
        Task ResolvePlayerEvents();
        List<List<double>> GetSinglesDistribution();
        List<List<double>> GetDoublesDistribution();
    }
}
