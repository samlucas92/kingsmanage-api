using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/club-access")]
public sealed class ClubAccessController : ControllerBase
{
	private readonly IUserService _userService;
	private readonly ISportsClubService _clubService;
	private readonly IJwtTokenService _jwtTokenService;
	private readonly ITenantContext _tenant;

	public ClubAccessController(
		IUserService userService,
		ISportsClubService clubService,
		IJwtTokenService jwtTokenService,
		ITenantContext tenant)
	{
		_userService = userService;
		_clubService = clubService;
		_jwtTokenService = jwtTokenService;
		_tenant = tenant;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<ClubAccessViewModel>>> GetAvailableClubs(
		CancellationToken cancellationToken)
	{
		var user = await GetCurrentUserAsync(cancellationToken);
		if (user is null) return Unauthorized();

		var clubs = await _clubService.GetAllAsync(cancellationToken);
		var accessibleClubIds = user.Memberships
			.Where(membership => membership.OrganizationId == _tenant.OrganizationId)
			.Where(membership => membership.ClubId.HasValue)
			.Select(membership => membership.ClubId!.Value)
			.ToHashSet();
		var hasOrganizationAccess = user.IsPlatformAdmin || user.Memberships.Any(membership =>
			membership.OrganizationId == _tenant.OrganizationId &&
			membership.ClubId == null &&
			membership.Role == TenantRole.OrganizationAdmin);

		return Ok(clubs
			.Where(club => club.IsActive && (hasOrganizationAccess || accessibleClubIds.Contains(club.Id)))
			.Select(club => new ClubAccessViewModel
			{
				Id = club.Id,
				Name = club.Name,
				SportKey = club.SportKey,
				IsCurrent = club.Id == _tenant.ClubId
			})
			.ToList());
	}

	[HttpPost("switch")]
	public async Task<ActionResult<LoginResponse>> SwitchClub(
		SwitchClubRequest request,
		CancellationToken cancellationToken)
	{
		if (request.ClubId == Guid.Empty) return BadRequest("Club is required.");

		var club = await _clubService.GetByIdAsync(request.ClubId, cancellationToken);
		if (club is null || !club.IsActive) return NotFound("Club was not found or is inactive.");

		var userId = GetCurrentUserId();
		if (!userId.HasValue) return Unauthorized();

		var user = await _userService.SetDefaultClubAsync(userId.Value, club.Id, cancellationToken);
		if (user is null) return Forbid();

		return Ok(_jwtTokenService.CreateLoginResponse(user));
	}

	private async Task<AppUser?> GetCurrentUserAsync(CancellationToken cancellationToken)
	{
		var userId = GetCurrentUserId();
		return userId.HasValue
			? await _userService.GetByIdAsync(userId.Value, cancellationToken)
			: null;
	}

	private Guid? GetCurrentUserId() =>
		Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
}
