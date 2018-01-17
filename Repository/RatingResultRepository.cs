using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversalTennis.Algorithm.Data;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Repository
{
    public class RatingResultRepository : IRatingResultRepository
    {
        private readonly UniversalTennisContext _context;

        public RatingResultRepository(
            UniversalTennisContext context
        )
        {
            _context = context;
        }

        public async Task<bool> Exists(Expression<Func<RatingResult, bool>> query)
        {
            return await _context.ResultRatings.AnyAsync(query);
        }

        public async Task<RatingResult> GetByResultId(int resultId)
        {
            return await _context.ResultRatings.FirstOrDefaultAsync(r => r.ResultId == resultId);
        }

        public async Task InsertList(List<RatingResult> resultRatings)
        {
            await _context.ResultRatings.AddRangeAsync(resultRatings);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteList(List<RatingResult> resultRatings)
        {
            _context.ResultRatings.RemoveRange(resultRatings);
            await _context.SaveChangesAsync();
        }
    }
}
