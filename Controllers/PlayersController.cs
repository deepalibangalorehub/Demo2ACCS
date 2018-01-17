using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UniversalTennis.Algorithm.Models;
using UniversalTennis.Algorithm.Service;

namespace UniversalTennis.Algorithm.Controllers
{
    [Route("api/[controller]")]
    public class PlayersController : Controller
    {
        private readonly IPlayerService _playerService;

        public PlayersController(
            IPlayerService playerService,
            IOptions<Config> config
        )
        {
            _playerService = playerService;
        }

        [HttpGet("eligibleresults", Name = "eligibleresults")]
        public async Task<ActionResult> EligibleResults(int id, string type)
        {
            if (type.Equals("singles", StringComparison.OrdinalIgnoreCase))
                return new JsonResult(await _playerService.GetEligibleSinglesResults(id));
            if (type.Equals("doubles", StringComparison.OrdinalIgnoreCase))
                return new JsonResult(await _playerService.GetEligibleDoublesResults(id));
            return StatusCode(400, "Type must be singles or doubles");
        }
    }
}
