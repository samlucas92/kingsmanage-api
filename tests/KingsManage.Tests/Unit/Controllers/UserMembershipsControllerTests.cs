using KingsManage;
using KingsManage.Web.Controllers;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

[TestFixture]
public sealed class UserMembershipsControllerTests
{
	[Test]
	public async Task Update_ReturnsConflictWhenFinalAdminWouldBeRemoved()
	{
		var controller = new UserMembershipsController(new StubService
		{
			Error = new InvalidOperationException("The final Organization Admin cannot be removed.")
		});

		var result = await controller.Update(Guid.NewGuid(), new UpdateMembershipsRequest
		{
			Memberships = [new UserMembership { Role = TenantRole.Player, ClubId = Guid.NewGuid() }]
		}, CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<ConflictObjectResult>());
	}

	[Test]
	public async Task Update_ReturnsBadRequestForInvalidAssignment()
	{
		var controller = new UserMembershipsController(new StubService
		{
			Error = new ArgumentException("A valid club is required for this role.")
		});

		var result = await controller.Update(Guid.NewGuid(), new UpdateMembershipsRequest(), CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	private sealed class StubService : IUserMembershipService
	{
		public Exception? Error { get; init; }
		public Task<IReadOnlyList<MembershipClubOption>> GetOptionsAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<MembershipClubOption>>([]);

		public Task<AppUser?> UpdateAsync(Guid userId, IReadOnlyList<UserMembership> memberships, Guid? defaultClubId, CancellationToken cancellationToken = default)
		{
			if (Error is not null) throw Error;
			return Task.FromResult<AppUser?>(new AppUser { Id = userId });
		}
	}
}
