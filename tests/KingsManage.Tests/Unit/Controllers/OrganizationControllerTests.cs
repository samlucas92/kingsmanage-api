using KingsManage;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

[TestFixture]
public sealed class OrganizationControllerTests
{
	[Test]
	public async Task CreateClub_RejectsMissingSport()
	{
		var controller = new OrganizationController(new StubOrganizationService(), new StubClubService());

		var result = await controller.CreateClub(
			new SportsClub { Name = "Kingsbridge", Slug = "kingsbridge", SportKey = "" },
			CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task CreateClub_ReturnsCreatedClub()
	{
		var clubs = new StubClubService();
		var controller = new OrganizationController(new StubOrganizationService(), clubs);

		var result = await controller.CreateClub(
			new SportsClub { Name = "Kingsbridge Rugby", Slug = "kingsbridge-rugby", SportKey = "rugby-union" },
			CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<CreatedResult>());
		Assert.That(clubs.CreatedClub?.Name, Is.EqualTo("Kingsbridge Rugby"));
	}

	private sealed class StubOrganizationService : IOrganizationService
	{
		public Task<Organization?> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult<Organization?>(new());
		public Task<Organization?> UpdateCurrentAsync(Organization organization, CancellationToken cancellationToken = default) => Task.FromResult<Organization?>(organization);
	}

	private sealed class StubClubService : ISportsClubService
	{
		public SportsClub? CreatedClub { get; private set; }
		public Task<IReadOnlyList<SportsClub>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SportsClub>>([]);
		public Task<SportsClub?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<SportsClub?>(null);
		public Task<SportsClub> CreateAsync(SportsClub club, CancellationToken cancellationToken = default) { CreatedClub = club; club.Id = Guid.NewGuid(); return Task.FromResult(club); }
		public Task<SportsClub?> UpdateAsync(Guid id, SportsClub club, CancellationToken cancellationToken = default) => Task.FromResult<SportsClub?>(club);
		public Task<SportsClub?> SetLogoFileAsync(Guid id, Guid? logoFileId, CancellationToken cancellationToken = default) => Task.FromResult<SportsClub?>(null);
		public Task<SportsClub?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) => Task.FromResult<SportsClub?>(null);
	}
}
