using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShoeShop.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "StaffOrAdmin")]
    public class StaffController : ControllerBase
    {
        // Example: staff can read and update but not delete
        [HttpGet("shoes")]
        public IActionResult GetShoes()
        {
            return Ok(new { message = "List of shoes (read)" });
        }

        [HttpPost("shoes/{id}/update")]
        public IActionResult UpdateShoe(int id)
        {
            return Ok(new { message = $"Update shoe {id}" });
        }

        [HttpDelete("shoes/{id}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult DeleteShoe(int id)
        {
            return Forbid();
        }
    }
}
