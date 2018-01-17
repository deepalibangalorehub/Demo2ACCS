using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniversalTennis.Algorithm.Data;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Repository
{
    public class PlayerRatingRepository : IPlayerRatingRepository
    {
        private readonly UniversalTennisContext _context;
        private readonly Config _config;

        public PlayerRatingRepository(
            UniversalTennisContext context,
            IOptions<Config> config
        )
        {
            _config = config.Value;
            _context = context;
        }

        public async Task<Dictionary<int, PlayerRating>> GetAll()
        {
            using (var conn = new SqlConnection(_config.ConnectionStrings.DefaultConnection))
            {
                return (await conn.QueryAsync<PlayerRating>("exec PlayerRating_GetAllForRating")).ToDictionary(x => x.PlayerId);
            }
            //_context.PlayerRatings.AsNoTracking().ToDictionaryAsync(x => x.PlayerId);
        }

        public async Task<List<PlayerRating>> GetInactive()
        {
            _context.Database.SetCommandTimeout(300);
            return await _context.PlayerRatings.Include(p => p.WeeklyAverages)
                .Where(p => p.RatingReliability <= 0 && p.InactiveRating == null && p.WeeklyAverages.Any(w => w.RatingStatus == RatingStatus.Rated))
                .ToListAsync();
        }

        public int Count(int level, Expression<Func<PlayerRating, bool>> query)
        {
            return _context.PlayerRatings.Count(query);
        }

        public List<RatingDistributionSingles> GetForSinglesDistribution()
        {
            return _context.PlayerRatings.Select(p => new RatingDistributionSingles
            {
                RatingReliability = p.RatingReliability,
                ActualRating = p.ActualRating,
                FinalRating = p.FinalRating
            }).ToList();
        }

        public List<RatingDistributionDoubles> GetForDoublesDistribution()
        {
            return _context.PlayerRatings.Select(p => new RatingDistributionDoubles
            {
                DoublesRating = p.DoublesRating,
                DoublesReliability = p.DoublesReliability,
                FinalDoublesRating = p.FinalDoublesRating
            }).ToList();
        }

        public async Task<bool> Exists(Expression<Func<PlayerRating, bool>> query)
        {
            return await _context.PlayerRatings.AnyAsync(query);
        }

        public async Task<PlayerRating> GetById(int id)
        {
            return await _context.PlayerRatings.SingleOrDefaultAsync(p => p.Id == id);
        }

        public async Task<PlayerRating> GetByPlayerId(int playerId)
        {
            return await _context.PlayerRatings.FirstOrDefaultAsync(p => p.PlayerId == playerId);
        }

        public async Task<List<PlayerRating>> GetByPlayerIds(int[] playerIds)
        {
            return await _context.PlayerRatings
                .Where(p => playerIds.Contains(p.PlayerId)).ToListAsync();
        }

        public async Task<PlayerRating> Insert(PlayerRating playerRating)
        {
            await _context.PlayerRatings.AddAsync(playerRating);
            await _context.SaveChangesAsync();
            return playerRating;
        }

        public async Task InsertList(List<PlayerRating> playerRatings)
        {
            await _context.PlayerRatings.AddRangeAsync(playerRatings);
            await _context.SaveChangesAsync();
        }

        public async Task Delete(PlayerRating playerRating)
        {
            // player history should cascade
            _context.PlayerRatings.Remove(playerRating);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteList(List<PlayerRating> playerRatings)
        {
            _context.PlayerRatings.RemoveRange(playerRatings);
            await _context.SaveChangesAsync();
        }
    }
}
