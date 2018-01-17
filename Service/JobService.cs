using System;
using System.Threading.Tasks;
using Hangfire;
using UniversalTennis.Algorithm.Repository;

namespace UniversalTennis.Algorithm.Service
{
    public class JobService : IJobService
    {
        private readonly IPlayerService _playerService;
        private readonly IResultService _resultService;
        private readonly IRatingJobRepository _ratingJobRepository;

        public JobService
        (
            IPlayerService playerService,
            IResultService resultService,
            IRatingJobRepository ratingJobRepository
        )
        {
            _playerService = playerService;
            _resultService = resultService;
            _ratingJobRepository = ratingJobRepository;
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task UpdateRatingsAndResults()
        {
            if (await AlgorithmIsRunning()) return;
            await _playerService.ResolvePlayerEvents();
            await _resultService.ResolveResultEvents();
        }

        public async Task<bool> AlgorithmIsRunning()
        {
            // checks for a rating job in the processing state
            var job = await _ratingJobRepository.GetActive();
            if (job == null) return false;
            using (var connection = JobStorage.Current.GetConnection())
            {
                try
                {
                    var jobState = connection.GetStateData(job.JobId.ToString());
                    if (jobState.Name.Equals(Hangfire.States.ProcessingState.StateName))
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    //job has not been run by the scheduler yet, swallow error
                    return false;
                }
            }
            return false;
        }
    }
}
