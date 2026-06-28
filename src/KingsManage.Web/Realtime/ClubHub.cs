using System.Security.Claims;
using KingsManage.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace KingsManage.Web.Realtime;

[Authorize]
public sealed class ClubHub : Hub
{
	public override async Task OnConnectedAsync()
	{
		var user = Context.User;

		if (
			user is null ||
			!TryReadGuid(user, HttpTenantContext.OrganizationClaim, out var organizationId) ||
			!TryReadGuid(user, HttpTenantContext.ClubClaim, out var clubId) ||
			!TryReadUserId(user, out var userId)
		)
		{
			Context.Abort();
			return;
		}

		await Groups.AddToGroupAsync(
			Context.ConnectionId,
			RealtimeGroups.Organization(organizationId)
		);
		await Groups.AddToGroupAsync(
			Context.ConnectionId,
			RealtimeGroups.Club(organizationId, clubId)
		);
		await Groups.AddToGroupAsync(
			Context.ConnectionId,
			RealtimeGroups.User(organizationId, clubId, userId)
		);

		await base.OnConnectedAsync();
	}

	private static bool TryReadGuid(
		ClaimsPrincipal user,
		string claimType,
		out Guid value
	) =>
		Guid.TryParse(user.FindFirstValue(claimType), out value) &&
		value != Guid.Empty;

	private static bool TryReadUserId(ClaimsPrincipal user, out Guid userId)
	{
		var value =
			user.FindFirstValue(ClaimTypes.NameIdentifier) ??
			user.FindFirstValue("sub") ??
			user.FindFirstValue("id");

		return Guid.TryParse(value, out userId) && userId != Guid.Empty;
	}
}
