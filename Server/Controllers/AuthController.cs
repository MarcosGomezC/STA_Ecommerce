using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")] // Rate limiting estricto para login
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email y contraseña son obligatorios" });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Intento de login fallido: usuario no encontrado {Email}", request.Email);
            // No revelar si el usuario existe o no
            await Task.Delay(Random.Shared.Next(100, 500)); // Timing attack mitigation
            return Unauthorized(new { error = "Credenciales inválidas" });
        }

        // Verificar si la cuenta está bloqueada
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Intento de login en cuenta bloqueada: {Email}", request.Email);
            return Unauthorized(new { error = "Cuenta bloqueada. Intenta más tarde." });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Intento de login fallido para {Email}. Lockout: {IsLockedOut}",
                request.Email, result.IsLockedOut);

            if (result.IsLockedOut)
            {
                return Unauthorized(new { error = "Cuenta bloqueada por múltiples intentos fallidos. Intenta en 15 minutos." });
            }

            await Task.Delay(Random.Shared.Next(100, 500)); // Timing attack mitigation
            return Unauthorized(new { error = "Credenciales inválidas" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? ""),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var jwtKey = _configuration["Jwt:Key"] ??
            throw new InvalidOperationException("JWT Key no configurada");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expirationHours = _configuration.GetValue<int>("Jwt:ExpirationHours", 8);
        var expires = DateTime.UtcNow.AddHours(expirationHours);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        _logger.LogInformation("Login exitoso para {Email}", request.Email);

        return Ok(new LoginResponse(tokenString, user.Email ?? "", roles));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        // Con JWT, el logout es principalmente del lado del cliente
        // Aquí podríamos agregar el token a una blacklist si fuera necesario
        await _signInManager.SignOutAsync();

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        _logger.LogInformation("Logout exitoso para {Email}", userEmail);

        return Ok(new { message = "Sesión cerrada exitosamente" });
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

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("api")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { error = "Usuario no encontrado" });
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new { error = "Error al cambiar la contraseña", errors = result.Errors });
        }

        _logger.LogInformation("Contraseña cambiada exitosamente para {Email}", user.Email);

        return Ok(new { message = "Contraseña cambiada exitosamente" });
    }
}

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);