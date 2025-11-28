using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STA_Ecommerce.Server.Services;
using STA_Ecommerce.Shared;

namespace STA_Ecommerce.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AnalyticsController : ControllerBase
{
    private readonly IClickTrackingService _trackingService;

    public AnalyticsController(IClickTrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    /// <summary>
    /// Obtiene estadísticas de clicks (solo admin)
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ClickStats>> GetStats([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        try
        {
            var stats = await _trackingService.GetStatsAsync(from, to);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene clicks recientes (solo admin)
    /// </summary>
    [HttpGet("recent-clicks")]
    public async Task<ActionResult<List<ClickTrackingDto>>> GetRecentClicks([FromQuery] int count = 50)
    {
        try
        {
            var clicks = await _trackingService.GetRecentClicksAsync(count);

            // Convertir a DTO para evitar problemas de serialización
            var dtos = clicks.Select(c => new ClickTrackingDto
            {
                Id = c.Id,
                ProductId = c.ProductId,
                ProductName = "", // Se llenará desde el cliente si es necesario
                Timestamp = c.Timestamp,
                IpAddress = c.IpAddress,
                Source = c.Source
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}