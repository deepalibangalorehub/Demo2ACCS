using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversalTennis.Algorithm.Data;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Repository
{
    public class RatingJobRepository : IRatingJobRepository
    {
        private readonly UniversalTennisContext _context;
       
        public RatingJobRepository(
            UniversalTennisContext context
        )
        {
            _context = context;
        }

        public async Task<RatingJob> GetById(int id)
        {
            return await _context.RatingJobs.FindAsync(id);
        }

        public async Task<RatingJob> GetActive(string type = null)
        {
            if (type == null)
                return await _context.RatingJobs.FirstOrDefaultAsync(r => r.EndTime == null);
            return await _context.RatingJobs.FirstOrDefaultAsync(r => r.EndTime == null && r.Type.Equals(type));
        }

        public async Task<RatingJob> GetLast(string type = null)
        {
            return await _context.RatingJobs
                .OrderByDescending(r => r.EndTime)
                .FirstOrDefaultAsync(r => r.EndTime != null && r.Type.Equals(type));
        }

        public async Task UpdateStatusById(int id, string status)
        {
            var entry = await _context.RatingJobs.FindAsync(id);
            entry.Status = status;
            await Update(entry);
        }

        public async Task<RatingJob> Insert(RatingJob job)
        {
            await _context.RatingJobs.AddAsync(job);
            await _context.SaveChangesAsync();
            return job;
        }

        public async Task Update(RatingJob job)
        {
            _context.RatingJobs.Update(job);
            await _context.SaveChangesAsync();
        }
    }
}
