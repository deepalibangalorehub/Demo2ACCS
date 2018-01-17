using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using UniversalTennis.Algorithm.Models;
using UniversalTennis.Algorithm.Repository;
using UniversalTennis.Algorithm.Service;

namespace UniversalTennis.Algorithm.Controllers
{
    [Route("api/[controller]")]
    public class AlgorithmController : Controller
    {
        private static IServiceProvider _serviceProvider;
        private readonly IRatingJobRepository _ratingJobRepository;
        private readonly IJobService _jobService;
        private readonly Config _config;

        public AlgorithmController(
            IServiceProvider serviceProvider,
            IRatingJobRepository ratingJobRepository,
            IJobService jobService,
            IOptions<Config> config
        )
        {
            _serviceProvider = serviceProvider;
            _ratingJobRepository = ratingJobRepository;
            _jobService = jobService;
            _config = config.Value;
        }

        // GET api/values/5
        [HttpGet("getsettings", Name = "getsettings")]
        public ActionResult GetSettings(string type)
        {
            string settingType;
            switch (type)
            {
                case "singles":
                    settingType = "V2Variables";
                    break;
                case "doubles":
                    settingType = "DoublesVariables";
                    break;
                default:
                    return StatusCode(400, "Type must be singles or doubles");
            }
            using (var conn = new SqlConnection(_config.ConnectionStrings.DefaultConnection))
            {
                var settings = conn.Query<AlgorithmSettings>("select Doc from algorithmsetting where type = @Type",
                    new { Type = settingType }).First();
                return new ObjectResult(settings.Doc);
            }
        }

        // GET api/values/5
        [HttpPost("setsettings", Name = "setsettings")]
        public ActionResult SetSettings(string type, [FromBody]RatingRule.V2Settings settings)
        {
            string settingType;
            switch (type)
            {
                case "singles":
                    settingType = "V2Variables";
                    break;
                case "doubles":
                    settingType = "DoublesVariables";
                    break;
                default:
                    return StatusCode(400, "Type must be singles or doubles");
            }
            using (var conn = new SqlConnection(_config.ConnectionStrings.DefaultConnection))
            {
                conn.Execute("update algorithmsetting set Doc = @Value where type = @Type",
                    new { Value = JsonConvert.SerializeObject(settings), Type = settingType });
                return StatusCode(200);
            }
        }

        // GET api/values/5
        [HttpGet("getcurve", Name = "getcurve")]
        public ActionResult GetCurveSettings(string type)
        {
            string settingType;
            switch (type)
            {
                case "singles":
                    settingType = "V2NormalizationCurve";
                    break;
                case "doubles":
                    settingType = "DoublesNormalizationCurve";
                    break;
                default:
                    return StatusCode(400, "Type must be singles or doubles");
            }
            using (var conn = new SqlConnection(_config.ConnectionStrings.DefaultConnection))
            {
                var settings = conn.Query<AlgorithmSettings>("select Doc from algorithmsetting where type = @Type",
                    new { Type = settingType }).First();
                return new ObjectResult(settings.Doc);
            }
        }

        // GET api/values/5
        [HttpPost("setcurve", Name = "setcurve")]
        public ActionResult SetCurveSettings(string type, [FromBody]V2NormalizationCurve settings)
        {
            string settingType;
            switch (type)
            {
                case "singles":
                    settingType = "V2NormalizationCurve";
                    break;
                case "doubles":
                    settingType = "DoublesNormalizationCurve";
                    break;
                default:
                    return StatusCode(400, "Type must be singles or doubles");
            }
            using (var conn = new SqlConnection(_config.ConnectionStrings.DefaultConnection))
            {
                conn.Execute("update algorithmsetting set Doc = @Value where type = @Type",
                    new { Value = JsonConvert.SerializeObject(settings), Type = settingType});
                return StatusCode(200);
            }
        }

        // GET api/values/5
        [HttpGet("getprogress", Name = "getprogress")]
        public async Task<string> GetProgress(string type)
        {
            string jobType;
            switch (type)
            {
                case "singles":
                    jobType = "Algorithm.Singles";
                    break;
                case "doubles":
                    jobType = "Algorithm.Doubles";
                    break;
                default:
                    return "Type must be singles or doubles";
            }
            var job = await _ratingJobRepository.GetActive(jobType);
            return job == null ? "Not running" : job.Status;
        }

        // GET api/values/5
        [HttpGet("lastrun", Name = "lastrun")]
        public async Task<string> GetLastRun(string type)
        {
            string jobType;
            switch (type)
            {
                case "singles":
                    jobType = "Algorithm.Singles";
                    break;
                case "doubles":
                    jobType = "Algorithm.Doubles";
                    break;
                default:
                    return "Type must be singles or doubles";
            }
            var job = await _ratingJobRepository.GetLast(jobType);
            return job?.EndTime == null
                ? "No runs found" 
                : $"Last run: {((DateTime)job.EndTime).ToString()}, Outcome: {job.Status}";
        }

        // POST api/values
        [HttpPost("run", Name = "run")]
        public async Task<IActionResult> Run(int iterations, string type)
        {
            var running = await _jobService.AlgorithmIsRunning();
            if (running) return StatusCode(409, "Algorithm is already running");
            var ratingJob = new RatingJob
            {
                JobId = 0,
                StartTime = DateTime.Now,
            };
            int jobId;
            switch (type)
            {
                case "singles":
                    ratingJob.Type = "Algorithm.Singles";
                    await _ratingJobRepository.Insert(ratingJob);
                    var singlesInstance = (Algorithm) _serviceProvider.GetService(typeof(Algorithm));
                    jobId = int.Parse(BackgroundJob.Enqueue(() => singlesInstance.UpdateRating(iterations, false, ratingJob.Id)));
                    break;
                case "doubles":
                    ratingJob.Type = "Algorithm.Doubles";
                    await _ratingJobRepository.Insert(ratingJob);
                    var doublesInstance = (AlgorithmDoubles)_serviceProvider.GetService(typeof(AlgorithmDoubles));
                    jobId = int.Parse(BackgroundJob.Enqueue(() => doublesInstance.UpdateRating(iterations, null, ratingJob.Id)));
                    break;
                default:
                    return StatusCode(400, "Type must be singles or doubles");
            }
            ratingJob.JobId = jobId;
            await _ratingJobRepository.Update(ratingJob);
            return StatusCode(200, "Algorithm started");
        }
    }
}
