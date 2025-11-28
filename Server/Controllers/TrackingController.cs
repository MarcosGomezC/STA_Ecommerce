using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using STA_Ecommerce.Server.Services;

namespace STA_Ecommerce.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrackingController : ControllerBase
{
    private readonly IClickTrackingService _trackingService;

    public TrackingController(IClickTrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    /// <summary>
    /// Registra un click en un producto (endpoint público, sin autenticación)
    /// </summary>
    [HttpPost("click/{productId:int}")]
    [AllowAnonymous]
    [EnableRateLimiting("api")]
    public async Task<IActionResult> TrackClick(int productId, [FromQuery] string? source = "Direct")
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();
            var referrer = Request.Headers["Referer"].ToString();

            await _trackingService.TrackClickAsync(productId, ipAddress, userAgent, referrer, source ?? "Direct");

            return Ok(new { success = true, message = "Click registrado" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}