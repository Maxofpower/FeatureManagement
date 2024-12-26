using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
	private readonly IConfiguration _configuration;

	public AuthController(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	[HttpPost("login")]
	public IActionResult Login([FromBody] LoginModel login)
	{
		if (ValidateVipUser(login.Username, login.Password))
		{
			var token = GenerateJwtToken(login.Username, isVip: true);
			return Ok(new { token });
		}
		else 
		{
			var token = GenerateJwtToken(login.Username, isVip: false);
			return Ok(new { token });
		}

		
	}

	private bool ValidateVipUser(string username, string password)
	{
		// Add your VIP user validation logic here
		return username == "vipuser" && password == "vippassword";
	}

	private string GenerateJwtToken(string username, bool isVip)
	{
		var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);
		var claims = new List<Claim>
		{
			new Claim(JwtRegisteredClaimNames.Sub, username),
			new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
		};

		if (isVip)
		{
			claims.Add(new Claim("VIP", "true"));
		}

		var tokenDescriptor = new SecurityTokenDescriptor
		{
			Subject = new ClaimsIdentity(claims),
			Expires = DateTime.UtcNow.AddHours(1),
			SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
			Issuer = _configuration["Jwt:Issuer"],
			Audience = _configuration["Jwt:Audience"]
		};

		var tokenHandler = new JwtSecurityTokenHandler();
		var token = tokenHandler.CreateToken(tokenDescriptor);

		return tokenHandler.WriteToken(token);
	}
}

public class LoginModel
{
	public string Username { get; set; }
	public string Password { get; set; }
}
