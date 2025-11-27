using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace STA_Ecommerce.Server.Controllers;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, string Email, IEnumerable<string> Roles);

public record AuthStatusResponse(bool IsAuthenticated, string? Email, IEnumerable<string> Roles);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _configuration;

    public AuthController(UserManager<IdentityUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Unauthorized(new { error = "Credenciales inválidas" });
        }

        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValidPassword)
        {
            return Unauthorized(new { error = "Credenciales inválidas" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? ""),
            new(ClaimTypes.Email, user.Email ?? "")
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var jwtKey = _configuration["Jwt:Key"] ?? "STA_Ecommerce_Secret_Key_Min_32_Characters_Long_For_Security";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(8);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new LoginResponse(tokenString, user.Email ?? "", roles));
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // Con JWT, el logout es del lado del cliente (eliminar el token)
        return Ok();
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<AuthStatusResponse> Me()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return new AuthStatusResponse(false, null, Array.Empty<string>());
        }

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity!.Name;
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        return new AuthStatusResponse(true, email, roles);
    }
}


