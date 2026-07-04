using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "OrganizationAdmin")]
[Route("api/club-setup")]
public sealed class ClubSetupController : ControllerBase
{
	private readonly IUserService _users;
	private readonly IClubTeamService _teams;
	private readonly ITenantContext _tenant;

	public ClubSetupController(
		IUserService users,
		IClubTeamService teams,
		ITenantContext tenant)
	{
		_users = users;
		_teams = teams;
		_tenant = tenant;
	}

	[HttpPost("staff")]
	public async Task<ActionResult<UserViewModel>> CreateStaff(
		CreateSetupStaffRequest request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Email))
			return BadRequest("Email is required.");
		if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
			return BadRequest("Temporary password must be at least 8 characters long.");
		if (request.Role is not (TenantRole.ClubAdmin or TenantRole.TeamManager or TenantRole.Coach))
			return BadRequest("Setup can only create club administrators, team managers or coaches.");
		if (request.Role == TenantRole.ClubAdmin && request.TeamId.HasValue)
			return BadRequest("Club administrators cannot be limited to a team.");
		if (request.Role == TenantRole.TeamManager && !request.TeamId.HasValue)
			return BadRequest("A team is required for a team manager.");
		if (request.TeamId.HasValue &&
			await _teams.GetByIdAsync(request.TeamId.Value, cancellationToken) is null)
			return BadRequest("The selected team does not belong to this club.");
		if (await _users.GetByEmailAsync(request.Email, cancellationToken) is not null)
			return Conflict("A user with this email already exists.");

		var user = new AppUser
		{
			Email = request.Email,
			Role = request.Role == TenantRole.ClubAdmin ? UserRole.Admin : UserRole.Coach,
			DefaultOrganizationId = _tenant.OrganizationId,
			DefaultClubId = _tenant.ClubId,
			IsActive = true,
			Memberships =
			[
				new UserMembership
				{
					OrganizationId = _tenant.OrganizationId,
					ClubId = _tenant.ClubId,
					TeamId = request.TeamId,
					Role = request.Role
				}
			]
		};

		var created = await _users.CreateAsync(user, request.Password, cancellationToken);
		return Created($"/api/users/{created.Id}", UserViewModel.FromUser(created));
	}
}

public sealed class CreateSetupStaffRequest
{
	public string Email { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
	public TenantRole Role { get; set; }
	public Guid? TeamId { get; set; }
}
