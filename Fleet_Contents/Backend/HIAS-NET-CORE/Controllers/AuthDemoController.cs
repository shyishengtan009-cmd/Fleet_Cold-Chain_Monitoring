using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HIAS_NET_CORE.Controllers;

/// <summary>
/// Stand-in for HIAS's real login system, which doesn't exist in this standalone demo.
/// Issues a JWT carrying the claims every Fleet endpoint expects (OrganizationId, RoleCode),
/// always for the same single demo org. Not real authentication — anyone hitting this
/// endpoint gets a token. Fine for a portfolio demo, not for anything with real users.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthDemoController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthDemoController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost("demo-login")]
    public IActionResult DemoLogin()
    {
        var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing from configuration.");
        var orgId = _config.GetValue("FleetSim:OrgId", 1);

        var claims = new[]
        {
            new Claim("OrganizationId", orgId.ToString()),
            new Claim("RoleCode", "1"),
            new Claim(ClaimTypes.Name, "Demo User"),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "fleet-demo",
            audience: "fleet-demo",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials);

        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    }
}
