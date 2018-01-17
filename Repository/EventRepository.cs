using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniversalTennis.Algorithm.Data;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Repository
{
    public class EventRepository : IEventRepository
    {
        private readonly UniversalTennisContext _context;

        public EventRepository(
            UniversalTennisContext context
        )
        {
            _context = context;
        }

        public async Task<List<PlayerEvent>> GetAllPlayerEvents()
        {
            return await _context.PlayerEvents.ToListAsync();
        }

        public async Task<List<ResultEvent>> GetAllResultEvents()
        {
            return await _context.ResultEvents.ToListAsync();
        }

        public async Task<PlayerEvent> InsertPlayerEvent(PlayerEvent playerEvent)
        {
            await _context.PlayerEvents.AddAsync(playerEvent);
            await _context.SaveChangesAsync();
            return playerEvent;
        }

        public async Task<ResultEvent> InsertResultEvent(ResultEvent resultEvent)
        {
            await _context.ResultEvents.AddAsync(resultEvent);
            await _context.SaveChangesAsync();
            return resultEvent;
        }

        public async Task DeletePlayerEvents(List<PlayerEvent> playerEvents)
        {
            _context.PlayerEvents.RemoveRange(playerEvents);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteResultEvents(List<ResultEvent> resultEvents)
        {
            _context.ResultEvents.RemoveRange(resultEvents);
            await _context.SaveChangesAsync();
        }
    }
}
