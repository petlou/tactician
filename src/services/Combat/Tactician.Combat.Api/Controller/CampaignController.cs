using Microsoft.AspNetCore.Mvc;

namespace Tactician.Combat.Api.Controller;

public class CampaignController : CombatControllerBase
{
    [HttpGet("hello")]
    public async Task<IActionResult> Get()
    {
        return Ok(new { name = "Hello Peter!" });
    }
}
