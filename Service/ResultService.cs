using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Newtonsoft.Json;
using UniversalTennis.Algorithm.Models;
using UniversalTennis.Algorithm.Repository;

namespace UniversalTennis.Algorithm.Service
{
    public class ResultService : IResultService
    {
        private readonly Config _config;
        private readonly IRatingResultRepository _ratingResultRepository;
        private readonly IPlayerRatingRepository _playerRatingRepository;
        private readonly IEventRepository _eventRepository;

        public ResultService(
            IOptions<Config> config,
            IRatingResultRepository ratingResultRepository,
            IPlayerRatingRepository playerRatingRepository,
            IEventRepository eventRepository
        )
        {
            _config = config.Value;
            _ratingResultRepository = ratingResultRepository;
            _eventRepository = eventRepository;
            _playerRatingRepository = playerRatingRepository;
        }

        public async Task<List<Result>> GetPlayerResultsFromYear(int playerId, string type, DateTime thresholdDate)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var host = _config.UniversalTennisApiHost;
                var version = _config.UniversalTennisApiVersion;
                var token = _config.UniversalTennisApiToken;
                var threshold = thresholdDate.ToString("dd MMM yyyy", CultureInfo.CreateSpecificCulture("en-US"));
                var response = await client.GetAsync(
                    $"{host}/{version}/player/{playerId}/ratingresults?token={token}&threshold={threshold}&type={type}",
                    HttpCompletionOption.ResponseHeadersRead
                );
                response.Headers.TransferEncodingChunked = true;
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpOperationException(
                        $"Failed to retrieve results for player - {response.StatusCode} - {response.ReasonPhrase}");
                }
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    using (var sr = new StreamReader(stream))
                    {
                        using (JsonReader reader = new JsonTextReader(sr))
                        {
                            var serializer = new JsonSerializer();
                            return serializer.Deserialize<List<Result>>(reader);
                        }
                    }
                }
            }
        }

        public async Task<Dictionary<int, List<Result>>> GetPlayerResultsFromYear(string type, DateTime thresholdDate)
        {
            var results = await GetResults(type, thresholdDate);
            var dictPlayers = new Dictionary<int, List<Result>>();
            // build player/result dictionary
            foreach (var result in results)
            {
                AddResult(result.Winner1Id, result, dictPlayers);
                AddResult(result.Loser1Id, result, dictPlayers);
                if (type.Equals("doubles", StringComparison.OrdinalIgnoreCase))
                {
                    AddResult(result.Winner2Id ?? 0, result, dictPlayers);
                    AddResult(result.Loser2Id ?? 0, result, dictPlayers);
                }
            }
            return dictPlayers;
        }

        private async Task<List<Result>> GetResults(string type, DateTime thresholdDate)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(20);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var host = _config.UniversalTennisApiHost;
                var version = _config.UniversalTennisApiVersion;
                var token = _config.UniversalTennisApiToken;
                var threshold = thresholdDate.ToString("dd MMM yyyy", CultureInfo.CreateSpecificCulture("en-US"));
                var response = await client.GetAsync(
                    $"{host}/{version}/result/forrating?token={token}&threshold={threshold}&type={type}",
                    HttpCompletionOption.ResponseHeadersRead
                );
                response.Headers.TransferEncodingChunked = true;
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpOperationException(
                        $"Failed to retrieve results for rating update - {response.StatusCode} - {response.ReasonPhrase}");
                }
                // deseriable as stream to avoid memory bloat
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    using (var sr = new StreamReader(stream))
                    {
                        using (JsonReader reader = new JsonTextReader(sr))
                        {
                            var serializer = new JsonSerializer();
                            return serializer.Deserialize<List<Result>>(reader);
                        }
                    }
                }
            }
        }

        private static void AddResult(int playerId, Result result, IDictionary<int, List<Result>> dictPlayers)
        {
            if (dictPlayers.ContainsKey(playerId))
            {
                dictPlayers[playerId].Add(result);
            }
            else
            {
                var r = new List<Result> {result};
                dictPlayers.Add(playerId, r);
            }
        }

        public async Task<RatingResult> LoadPlayerResults(int resultId)
        {
            throw new NotImplementedException();
        }

        public async Task ResolveResultEvents()
        {
            var events = await _eventRepository.GetAllResultEvents();
            events = events.Take(6000).OrderBy(e => e.DateCreated).ToList();
            var additionList = new List<RatingResult>();
            var removalList = new List<RatingResult>();
            foreach (var e in events)
            {
                switch (e.Type)
                {
                    case ResultEventType.Created:
                        if (await _ratingResultRepository.Exists(r => r.ResultId == e.ResultId) ||
                            additionList.Any(r => r.ResultId == e.ResultId)) continue;
                        // add current player ratings and create result entry
                        e.Info = JsonConvert.DeserializeObject<ResultEventInfo>(e.InfoDoc);                 
                        // set the current ratings
                        additionList.Add(await UpdateRatingResult(e.ResultId, e.Info));
                        break;
                    case ResultEventType.Deleted:
                        if (removalList.Any(r => r.ResultId == e.ResultId)) continue;
                        var entry = await _ratingResultRepository.GetByResultId(e.ResultId);
                        if (entry != null) removalList.Add(entry);
                        break;
                    case ResultEventType.Updated:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            await _ratingResultRepository.InsertList(additionList);
            await _ratingResultRepository.DeleteList(removalList);
            // remove all the events
            await _eventRepository.DeleteResultEvents(events);
        }

        private async Task<RatingResult> UpdateRatingResult(int resultId, ResultEventInfo info)
        {
            // TODO: case where player doesn't exist
            var winner1 = await _playerRatingRepository.GetByPlayerId(info.Winner1Id);
            var winner2 = info.Winner2Id == null ? null : await _playerRatingRepository.GetByPlayerId((int)info.Winner2Id);
            var loser1 = await _playerRatingRepository.GetByPlayerId(info.Loser1Id);
            var loser2 = info.Loser2Id == null ? null : await _playerRatingRepository.GetByPlayerId((int)info.Loser2Id);
            return new RatingResult
            {
                ResultId = resultId,
                Winner1Rating = winner1?.FinalRating,
                Winner1Reliability = winner1?.Reliability,
                Winner1CalculatedRating = winner1?.Rating,
                Winner2Rating = winner2?.FinalRating,
                Winner2Reliability = winner2?.Reliability,
                Winner2CalculatedRating = winner2?.Rating,
                Loser1Rating = loser1?.FinalRating,
                Loser1Reliability = loser1?.Reliability,
                Loser1CalculatedRating = loser1?.Rating,
                Loser2Rating = loser2?.FinalRating,
                Loser2Reliability = loser2?.Reliability,
                Loser2CalculatedRating = loser2?.Rating
            };
        }
    }
}
