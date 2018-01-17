using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Repository
{
    public interface IRatingResultRepository
    {
        Task<RatingResult> GetByResultId(int resultId);
        Task<bool> Exists(Expression<Func<RatingResult, bool>> query);
        Task InsertList(List<RatingResult> resultRatings);
        Task DeleteList(List<RatingResult> resultRatings);
    }
}
