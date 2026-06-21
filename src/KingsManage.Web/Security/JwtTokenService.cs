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
		var organizationId = user.DefaultOrganizationId ??
			user.Memberships.Select(membership => membership.OrganizationId).FirstOrDefault();
		var clubId = user.DefaultClubId ??
			user.Memberships.Select(membership => membership.ClubId).FirstOrDefault(id => id.HasValue);

		// Existing users are assigned to Kingsbridge by the tenancy migration. The
		// fallback keeps tokens available during a rolling deployment.
		organizationId = organizationId == Guid.Empty ? DefaultTenant.OrganizationId : organizationId;
		clubId = !clubId.HasValue || clubId.Value == Guid.Empty ? DefaultTenant.ClubId : clubId;

		var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);
		var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
		var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
		TenantRole? tenantRole = user.Memberships
			.Where(membership => membership.OrganizationId == organizationId)
			.Where(membership => membership.ClubId == null || membership.ClubId == clubId)
			.OrderBy(membership => membership.Role)
			.Select(membership => (TenantRole?)membership.Role)
			.FirstOrDefault();

		if (user.Memberships.Count == 0)
		{
			tenantRole = user.Role switch
			{
				UserRole.Admin => TenantRole.OrganizationAdmin,
				UserRole.Coach => TenantRole.Coach,
				_ => TenantRole.Player
			};
		}

		if (!tenantRole.HasValue)
		{
			throw new InvalidOperationException("The user does not have a membership for their default organization and club.");
		}

		var effectiveRole = tenantRole.Value switch
		{
			TenantRole.OrganizationAdmin or TenantRole.ClubAdmin => UserRole.Admin,
			TenantRole.TeamManager or TenantRole.Coach => UserRole.Coach,
			_ => UserRole.Player
		};
		var claims = new List<Claim>
		{
			new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(JwtRegisteredClaimNames.Email, user.Email),
			new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Email, user.Email),
			new(ClaimTypes.Role, effectiveRole.ToString()),
			new(HttpTenantContext.OrganizationClaim, organizationId.ToString()),
			new(HttpTenantContext.ClubClaim, clubId.Value.ToString())
		};

		claims.Add(new Claim(HttpTenantContext.TenantRoleClaim, tenantRole.Value.ToString()));
		claims.Add(new Claim(HttpTenantContext.PlatformAdminClaim, user.IsPlatformAdmin.ToString().ToLowerInvariant()));

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
			User = UserViewModel.FromUser(user, tenantRole)
		};
	}
}
