using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Newtonsoft.Json;
using UniversalTennis.Algorithm.Models;
using UniversalTennis.Algorithm.Repository;
using UniversalTennis.DataObjects.Json;

namespace UniversalTennis.Algorithm.Service
{
    public class PlayerService : IPlayerService
    {
        private readonly Config _config;
        private readonly IPlayerRatingRepository _playerRatingRepository;
        private readonly IRatingHistoryRepository _ratingHistoryRepository;
        private readonly IEventRepository _eventRepository;

        public PlayerService(
            IOptions<Config> config,
            IPlayerRatingRepository playerRatingRepository,
            IRatingHistoryRepository ratingHistoryRepository,
            IEventRepository eventRepository
        )
        {
            _config = config.Value;
            _playerRatingRepository = playerRatingRepository;
            _ratingHistoryRepository = ratingHistoryRepository;
            _eventRepository = eventRepository;
        }

        public async Task<int[]> GetEligibleSinglesResults(int playerId)
        {
            var pr = await _playerRatingRepository.GetByPlayerId(playerId);
            return string.IsNullOrEmpty(pr?.ActiveSinglesResults) 
                ? new int[0] 
                : JsonConvert.DeserializeObject<int[]>(pr.ActiveSinglesResults);
        }

        public async Task<int[]> GetEligibleDoublesResults(int playerId)
        {
            var pr = await _playerRatingRepository.GetByPlayerId(playerId);
            return string.IsNullOrEmpty(pr?.ActiveDoublesResults)
                ? new int[0]
                : JsonConvert.DeserializeObject<int[]>(pr.ActiveDoublesResults);
        }

        public async Task<Player> GetPlayerInfo(int playerId)
        {
            throw new NotImplementedException();
        }

        public async Task<List<Player>> GetPlayerInfo(int[] playerIds)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var host = _config.UniversalTennisApiHost;
                var version = _config.UniversalTennisApiVersion;
                var token = _config.UniversalTennisApiToken;           
                var response = await client.PostAsync(
                    $"{host}/{version}/player/ratingprofiles?token={token}",                   
                    new StringContent(JsonConvert.SerializeObject(playerIds), Encoding.UTF8, "application/json")              
                );
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpOperationException($"Failed to retrieve player info - {response.StatusCode} - {response.ReasonPhrase}");
                }
                var content = await response.Content.ReadAsStringAsync();
                var players = JsonConvert.DeserializeObject<List<Player>>(content);
                await LoadPlayerRatings(players);
                return players;
            }
        }

        public async Task<List<Player>> GetPlayersWithResults(string type, DateTime thresholdDate)
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
                    $"{host}/{version}/player/activeforrating?token={token}&threshold={threshold}&type={type}",
                    HttpCompletionOption.ResponseHeadersRead
                );
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpOperationException($"Failed to retrieve players for rating update - {response.StatusCode} - {response.ReasonPhrase}");
                }
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    using (var sr = new StreamReader(stream))
                    {
                        using (JsonReader reader = new JsonTextReader(sr))
                        {
                            var rnd = new Random();
                            var serializer = new JsonSerializer();
                            var players = 
                                serializer.Deserialize<List<Player>>(reader).OrderBy(item => rnd.Next()).ToList();
                            await LoadPlayerRatingsForAlgorithm(players, type);
                            return players;
                        }
                    }
                }
            }
        }

        public async Task<List<Player>> LoadPlayerRatingsForAlgorithm(List<Player> players, string type)
        {
            var playerRatings = await _playerRatingRepository.GetAll();
            foreach (var player in players)
            {
                if (playerRatings.ContainsKey(player.Id))
                {
                    player.Stats = playerRatings[player.Id];
                    // remove them so they aren't reset
                    playerRatings.Remove(player.Id);
                }
                else
                {
                    // in case playerrating entry doesn't exist, create one now
                    var newPlayerRating = new PlayerRating {PlayerId = player.Id, PlayerGender = player.Gender};
                    await _playerRatingRepository.Insert(newPlayerRating);
                    player.Stats = newPlayerRating;
                }
            }
            // anybody left over should be reset (inactive)
            if (type.Equals("singles", StringComparison.OrdinalIgnoreCase))
                await ResetSinglesPlayerRatings(playerRatings.Values.ToList());
            else if (type.Equals("doubles", StringComparison.OrdinalIgnoreCase))
                await ResetDoublesPlayerRatings(playerRatings.Values.ToList());
            return players;
        }

        public async Task<List<Player>> LoadPlayerRatings(List<Player> players)
        {
            var playerRatings = (await _playerRatingRepository
                    .GetByPlayerIds(players.Select(p => p.Id).ToArray()))
                .ToDictionary(p => p.PlayerId);
            foreach (var player in players)
            {
                player.Stats = playerRatings[player.Id];
            }
            return players;
        }

        public async Task ResetSinglesPlayerRatings(List<PlayerRating> players)
        {
            // remove players that are already reset
            players.RemoveAll(p => (p.FinalRating <= 0 || p.FinalRating == null) && 
                                    (p.RatingReliability <= 0 || p.FinalRating == null));
            const string updateQuery = 
                "UPDATE PlayerRating Set FinalRating = 0, ActiveSinglesResults = NULL, RatingReliability = 0 WHERE Id = @Id";
            const string inactiveQuery =
                "UPDATE PlayerRating Set InactiveRating = @Rating WHERE Id = @Id";
            using (var conn = new SqlConnection(_config.ConnectionStrings.DefaultConnection))
            {
                await conn.ExecuteAsync(updateQuery, players);
                // set last known weekly average rating for inactive players
                var inactives = await _playerRatingRepository.GetInactive();
                foreach (var i in inactives)
                {
                    await conn.ExecuteAsync(inactiveQuery,
                        new { Id = i.Id, Rating = i.WeeklyAverages.OrderByDescending(w => w.Date).FirstOrDefault()?.Rating});
                }          
            }
        }

        public async Task ResetDoublesPlayerRatings(List<PlayerRating> players)
        {
            // remove player that are already reset
            players.RemoveAll(p => (p.FinalDoublesRating <= 0 || p.FinalDoublesRating == null) &&
                                   (p.DoublesReliability <= 0 || p.DoublesReliability == null));
            const string updateQuery =
                "UPDATE PlayerRating Set FinalDoublesRating = 0, ActiveDoublesResults = NULL, DoublesReliability = 0 WHERE Id = @Id";
            using (var conn = new SqlConnection(_config.ConnectionStrings.DefaultConnection))
            {
                await conn.ExecuteAsync(updateQuery, players);
            }
        }

        public async Task ResolvePlayerEvents()
        {
            var events = await _eventRepository.GetAllPlayerEvents();
            events = events.OrderBy(e => e.DateCreated).ToList(); // Important that we do this in chronological order
            foreach (var e in events)
            {               
                switch (e.Type) {
                    case PlayerEventType.Created:                       
                        if (await _playerRatingRepository.Exists(p => p.PlayerId == e.PlayerId)) continue;
                        var createInfo = JsonConvert.DeserializeObject<PlayerEventDoc>(e.InfoDoc);
                        await _playerRatingRepository.Insert(new PlayerRating {PlayerId = e.PlayerId, PlayerGender = createInfo.Gender});
                        break;
                    case PlayerEventType.Deleted:
                        var entry = await _playerRatingRepository.GetByPlayerId(e.PlayerId);
                        if (entry != null) await _playerRatingRepository.Delete(entry);
                        break;
                    case PlayerEventType.Merged:
                        var mergeInfo = JsonConvert.DeserializeObject<PlayerEventDoc>(e.InfoDoc);
                        // need to insert the target playerrating before merging history
                        var target = await _playerRatingRepository.GetByPlayerId((int)mergeInfo.TargetPlayerId);
                        if (target == null)
                        {
                            target = await _playerRatingRepository.Insert(new PlayerRating
                            {
                                ActualRating = mergeInfo.Rating,
                                Reliability = mergeInfo.Reliability ?? 0,
                                PlayerId = (int) mergeInfo.TargetPlayerId,
                                PlayerGender = mergeInfo.Gender
                            });
                        }
                        // merge rating history and delete the source players
                        var mergedDailys = _ratingHistoryRepository.MergeDailyRatings(mergeInfo.SourcePlayerIds, target.Id);
                        _ratingHistoryRepository.MergeWeeklyAverage(mergeInfo.SourcePlayerIds, target.Id, mergedDailys);
                        // delete the source players
                        foreach (var playerId in mergeInfo.SourcePlayerIds)
                        {
                            var mergedEntry = await _playerRatingRepository.GetByPlayerId(playerId);
                            if (mergedEntry != null) await _playerRatingRepository.Delete(mergedEntry);
                        }                        
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            // clear all the events
            await _eventRepository.DeletePlayerEvents(events);
        }

        public List<List<double>> GetSinglesDistribution()
        {
            var levels = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var data = new List<double> { 0 };
            var data2 = new List<double> { 0 };
            var players = _playerRatingRepository.GetForSinglesDistribution();
            for (var i = 1; i < levels.Count - 1; i++)
            {
                var level = levels[i];
                data.Add(players.Count(p => (int)Math.Round(p.ActualRating ?? -1, 0) == level && p.RatingReliability > 0));
                data2.Add(players.Count(p => (int)Math.Round(p.FinalRating ?? -1, 0) == level && p.RatingReliability > 0));
            }
            return new List<List<double>>() { data, data2 };
        }

        public List<List<double>> GetDoublesDistribution()
        {
            var levels = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var data = new List<double> { 0 };
            var data2 = new List<double> { 0 };
            var players = _playerRatingRepository.GetForDoublesDistribution();
            for (var i = 1; i < levels.Count - 1; i++)
            {
                var level = levels[i];
                data.Add(players.Count(p => (int)Math.Round(p.DoublesRating ?? -1, 0) == level && p.DoublesReliability > 0));
                data2.Add(players.Count(p => (int)Math.Round(p.FinalDoublesRating ?? -1, 0) == level && p.DoublesReliability > 0));
            }
            return new List<List<double>>() { data, data2 };
        }
    }
}
