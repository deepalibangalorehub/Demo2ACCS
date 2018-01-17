using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Service
{
    public interface IResultService
    {
        Task<List<Result>> GetPlayerResultsFromYear(int playerId, string type, DateTime thresholdDate);
        Task<Dictionary<int, List<Result>>> GetPlayerResultsFromYear(string type, DateTime thresholdDate);
        Task ResolveResultEvents();
    }
}
