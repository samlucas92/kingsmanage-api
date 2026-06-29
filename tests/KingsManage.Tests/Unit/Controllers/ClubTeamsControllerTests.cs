using KingsManage;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public class ClubTeamsControllerTests
{
	[Test]
	public async Task GetAll_ReturnsProfilesFromService()
	{
		var service = new FakeClubTeamService();
		service.Profiles.Add(CreateProfile("Senior Team"));
		var controller = new ClubTeamsController(service);

		var result = await controller.GetAll(CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var profiles = okResult?.Value as IReadOnlyList<ClubTeamProfile>;
		Assert.That(okResult, Is.Not.Null);
		Assert.That(profiles, Has.Count.EqualTo(1));
		Assert.That(profiles![0].DisplayName, Is.EqualTo("Senior Team"));
	}

	[Test]
	public async Task Update_WithUnknownTeam_ReturnsBadRequest()
	{
		var controller = new ClubTeamsController(new FakeClubTeamService());

		var result = await controller.Update(
			"Third",
			CreateProfile("Third Team"),
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Update_WithMissingDisplayName_ReturnsBadRequest()
	{
		var service = new FakeClubTeamService();
		var controller = new ClubTeamsController(service);
		var profile = CreateProfile(" ");

		var result = await controller.Update(
			Guid.NewGuid().ToString(),
			profile,
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
		Assert.That(service.UpdateCalls, Is.EqualTo(0));
	}

	[Test]
	public async Task Create_AddsAnotherTeam()
	{
		var service = new FakeClubTeamService();
		var controller = new ClubTeamsController(service);
		var profile = CreateProfile("Under 18s");
		profile.Competitions = ["Youth League", "Youth Cup"];

		var result = await controller.Create(
			profile,
			CancellationToken.None
		);

		var createdResult = result.Result as CreatedResult;
		var createdProfile = createdResult?.Value as ClubTeamProfile;
		Assert.That(createdResult, Is.Not.Null);
		Assert.That(createdProfile?.DisplayName, Is.EqualTo("Under 18s"));
		Assert.That(createdProfile?.Competitions, Is.EqualTo(new[] { "Youth League", "Youth Cup" }));
		Assert.That(service.CreateCalls, Is.EqualTo(1));
	}

	[Test]
	public async Task Delete_WhenTeamIsUnused_ReturnsNoContent()
	{
		var service = new FakeClubTeamService { DeleteResult = ClubTeamDeleteResult.Deleted };
		var controller = new ClubTeamsController(service);

		var result = await controller.Delete(Guid.NewGuid().ToString(), CancellationToken.None);

		Assert.That(result, Is.TypeOf<NoContentResult>());
	}

	[Test]
	public async Task Delete_WhenTeamIsInUse_ReturnsConflict()
	{
		var service = new FakeClubTeamService { DeleteResult = ClubTeamDeleteResult.InUse };
		var controller = new ClubTeamsController(service);

		var result = await controller.Delete(Guid.NewGuid().ToString(), CancellationToken.None);

		Assert.That(result, Is.TypeOf<ConflictObjectResult>());
	}

	private static ClubTeamProfile CreateProfile(string displayName)
	{
		return new ClubTeamProfile
		{
			DisplayName = displayName,
			ShortName = "Team",
			IsActive = true,
			SortOrder = 0
		};
	}

	private sealed class FakeClubTeamService : IClubTeamService
	{
		public List<ClubTeamProfile> Profiles { get; } = [];
		public int UpdateCalls { get; private set; }
		public int CreateCalls { get; private set; }
		public ClubTeamDeleteResult DeleteResult { get; set; } = ClubTeamDeleteResult.NotFound;

		public Task<IReadOnlyList<ClubTeamProfile>> GetAllAsync(
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<ClubTeamProfile>>(Profiles);
		}

		public Task<ClubTeamProfile?> GetByIdAsync(
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(Profiles.SingleOrDefault(profile => profile.Id == id));
		}

		public Task<ClubTeamProfile> CreateAsync(
			ClubTeamProfile profile,
			CancellationToken cancellationToken = default
		)
		{
			CreateCalls++;
			profile.Id = Guid.NewGuid();
			Profiles.Add(profile);
			return Task.FromResult(profile);
		}

		public Task<ClubTeamProfile> UpdateAsync(
			Guid id,
			ClubTeamProfile profile,
			CancellationToken cancellationToken = default
		)
		{
			UpdateCalls++;
			profile.Id = id;
			return Task.FromResult(profile);
		}

		public Task<ClubTeamDeleteResult> DeleteAsync(
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(DeleteResult);
		}
	}
}
