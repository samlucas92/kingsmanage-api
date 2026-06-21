using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "OrganizationAdmin")]
[Route("api/user-memberships")]
public sealed class UserMembershipsController : ControllerBase
{
	private readonly IUserMembershipService _service;

	public UserMembershipsController(IUserMembershipService service) => _service = service;

	[HttpGet("options")]
	public async Task<ActionResult<IReadOnlyList<MembershipClubOption>>> GetOptions(CancellationToken cancellationToken) =>
		Ok(await _service.GetOptionsAsync(cancellationToken));

	[HttpPut("{userId:guid}")]
	public async Task<ActionResult<UserViewModel>> Update(Guid userId, UpdateMembershipsRequest request, CancellationToken cancellationToken)
	{
		try
		{
			var user = await _service.UpdateAsync(userId, request.Memberships, request.DefaultClubId, cancellationToken);
			return user is null ? NotFound() : Ok(UserViewModel.FromUser(user));
		}
		catch (ArgumentException exception)
		{
			return BadRequest(exception.Message);
		}
		catch (InvalidOperationException exception)
		{
			return Conflict(exception.Message);
		}
	}
}
