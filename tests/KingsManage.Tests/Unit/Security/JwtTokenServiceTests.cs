using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Security;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace KingsManage.Tests.Unit.Security;

[TestFixture]
public class JwtTokenServiceTests
{
	[Test]
	public void CreateLoginResponse_ShouldCreateTokenAndUserViewModel()
	{
		var user = new AppUser
		{
			Id = Guid.NewGuid(),
			Email = "admin@test.local",
			Role = UserRole.Admin,
			PlayerId = Guid.NewGuid(),
			IsActive = true,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		var service = CreateService();

		var response = service.CreateLoginResponse(user);

		Assert.That(response.Token, Is.Not.Empty);
		Assert.That(response.ExpiresAt, Is.GreaterThan(DateTime.UtcNow));
		Assert.That(response.User.Id, Is.EqualTo(user.Id));
		Assert.That(response.User.Email, Is.EqualTo(user.Email));
		Assert.That(response.User.Role, Is.EqualTo(UserRole.Admin));
		Assert.That(response.User.PlayerId, Is.EqualTo(user.PlayerId));
	}

	[Test]
	public void CreateLoginResponse_ShouldIncludeRoleAndPlayerClaims()
	{
		var playerId = Guid.NewGuid();
		var user = new AppUser
		{
			Id = Guid.NewGuid(),
			Email = "player@test.local",
			Role = UserRole.Player,
			PlayerId = playerId,
			IsActive = true,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		var service = CreateService();

		var response = service.CreateLoginResponse(user);
		var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);

		Assert.That(token.Claims.Any(claim => claim.Type == ClaimTypes.Role && claim.Value == UserRole.Player.ToString()), Is.True);
		Assert.That(token.Claims.Any(claim => claim.Type == ClaimTypes.NameIdentifier && claim.Value == user.Id.ToString()), Is.True);
		Assert.That(token.Claims.Any(claim => claim.Type == ClaimTypes.Email && claim.Value == user.Email), Is.True);
		Assert.That(token.Claims.Any(claim => claim.Type == "playerId" && claim.Value == playerId.ToString()), Is.True);
	}

	private static JwtTokenService CreateService()
	{
		var settings = new JwtSettings
		{
			Issuer = "KingsManage.Tests",
			Audience = "KingsManage.Tests.Frontend",
			Secret = "test-secret-with-enough-length-for-hmac-sha256",
			ExpiryMinutes = 60
		};

		return new JwtTokenService(Options.Create(settings));
	}
}
