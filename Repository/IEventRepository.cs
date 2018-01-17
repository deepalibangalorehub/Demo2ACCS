using System.Collections.Generic;
using System.Threading.Tasks;
using UniversalTennis.Algorithm.Models;

namespace UniversalTennis.Algorithm.Repository
{
    public interface IEventRepository
    {
        Task<List<PlayerEvent>> GetAllPlayerEvents();
        Task<List<ResultEvent>> GetAllResultEvents();
        Task<PlayerEvent> InsertPlayerEvent(PlayerEvent playerEvent);
        Task<ResultEvent> InsertResultEvent(ResultEvent resultEvent);
        Task DeletePlayerEvents(List<PlayerEvent> playerEvents);
        Task DeleteResultEvents(List<ResultEvent> resultEvents);
    }
}
