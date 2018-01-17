using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversalTennis.Algorithm.Data;
using UniversalTennis.Algorithm.Extensions;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Repository
{
    public class SubRatingRepository : ISubRatingRepository
    {
        private readonly UniversalTennisContext _context;

        public SubRatingRepository(
            UniversalTennisContext context
        )
        {
            _context = context;
        }

        public async Task AddOrUpdateSubRating(SubRating subRating)
        {
            var existing = await _context.SubRatings
                .FirstOrDefaultAsync(s => s.PlayerRatingId == subRating.PlayerRatingId);
            if (existing != null)
            {
                existing.DateLastUpdated = DateTime.Now;
                existing.HardCourt = subRating.HardCourt;
                existing.ClayCourt = subRating.ClayCourt;
                existing.GrassCourt = subRating.GrassCourt;
                existing.OneMonth = subRating.OneMonth;
                existing.ThreeMonth = subRating.ThreeMonth;
                existing.EightWeek = subRating.EightWeek;
                existing.SixWeek = subRating.SixWeek;
                existing.GrandSlamMasters = subRating.GrandSlamMasters;
                existing.ResultCount = subRating.ResultCount;
                existing.HardCourtCount = subRating.HardCourtCount;
                existing.ClayCourtCount = subRating.ClayCourtCount;
                existing.GrassCourtCount = subRating.GrassCourtCount;
                existing.OneMonthCount = subRating.OneMonthCount;
                existing.ThreeMonthCount = subRating.ThreeMonthCount;
                existing.EightWeekCount = subRating.EightWeekCount;
                existing.SixWeekCount = subRating.SixWeekCount;
                existing.GrandSlamMastersCount = subRating.GrandSlamMastersCount;
            }
            else
            {
                subRating.DateCreated = DateTime.Now;
                subRating.DateLastUpdated = DateTime.Now;
                await _context.SubRatings.AddAsync(subRating);
            }
            await _context.SaveChangesAsync();          
        }

        public SubRatingsWithRankings GetPlayerSubRatingsAndRankings(int playerId)
        {
            var results = _context.LoadStoredProc("Player_GetSubUTRs_ById")
                .WithSqlParam("@playerId", playerId)
                .ExecuteStoredProc<SubRatingsWithRankings>();
            return results.FirstOrDefault();
        }
    }
}
