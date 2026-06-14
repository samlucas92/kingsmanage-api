using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KingsManage;
using KingsManage.Web.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KingsManage.Web.Security;

public interface IJwtTokenService
{
	LoginResponse CreateLoginResponse(AppUser user);
}

public sealed class JwtTokenService : IJwtTokenService
{
	private readonly JwtSettings _jwtSettings;

	public JwtTokenService(IOptions<JwtSettings> jwtSettings)
	{
		_jwtSettings = jwtSettings.Value;
	}

	public LoginResponse CreateLoginResponse(AppUser user)
	{
		var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);
		var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
		var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
		var claims = new List<Claim>
		{
			new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
			new(JwtRegisteredClaimNames.Email, user.Email),
			new(ClaimTypes.NameIdentifier, user.Id.ToString()),
			new(ClaimTypes.Email, user.Email),
			new(ClaimTypes.Role, user.Role.ToString())
		};

		if (user.PlayerId.HasValue)
		{
			claims.Add(new Claim("playerId", user.PlayerId.Value.ToString()));
		}

		var token = new JwtSecurityToken(
			issuer: _jwtSettings.Issuer,
			audience: _jwtSettings.Audience,
			claims: claims,
			expires: expiresAt,
			signingCredentials: credentials
		);

		return new LoginResponse
		{
			Token = new JwtSecurityTokenHandler().WriteToken(token),
			ExpiresAt = expiresAt,
			User = UserViewModel.FromUser(user)
		};
	}
}
