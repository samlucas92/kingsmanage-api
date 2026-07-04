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

	[Test]
	public async Task CreateClub_WhenSubscriptionHasNoCapacity_ReturnsPaymentRequired()
	{
		var controller = new OrganizationController(
			new StubOrganizationService(),
			new StubClubService(),
			new StubBillingService(canAddClub: false));

		var result = await controller.CreateClub(
			new SportsClub
			{
				Name = "Another Club",
				Slug = "another-club",
				SportKey = "football"
			},
			CancellationToken.None);

		Assert.That((result.Result as ObjectResult)?.StatusCode, Is.EqualTo(402));
	}

	[Test]
	public async Task UpdateClub_AcceptsACompleteCustomFormation()
	{
		var club = new SportsClub
		{
			Name = "Kingsbridge",
			Slug = "kingsbridge",
			SportKey = "football",
			CustomFormations =
			[
				new ClubFormation
				{
					Key = "custom-shape",
					Name = "Custom shape",
					Slots = Enumerable.Range(0, 11).Select(index => new ClubFormationSlot
					{
						Key = $"slot-{index}",
						Label = index == 0 ? "GK" : "CM",
						X = 10 + index * 7,
						Y = 50
					}).ToList()
				}
			]
		};
		var controller = new OrganizationController(new StubOrganizationService(), new StubClubService());

		var result = await controller.UpdateClub(Guid.NewGuid(), club, CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
	}

	[Test]
	public async Task UpdateClub_RejectsAnIncompleteCustomFormation()
	{
		var club = new SportsClub
		{
			Name = "Kingsbridge",
			Slug = "kingsbridge",
			SportKey = "rugby-league",
			CustomFormations =
			[
				new ClubFormation
				{
					Key = "short-team",
					Name = "Short team",
					Slots = [new ClubFormationSlot { Key = "fullback", Label = "FB", X = 50, Y = 10 }]
				}
			]
		};
		var controller = new OrganizationController(new StubOrganizationService(), new StubClubService());

		var result = await controller.UpdateClub(Guid.NewGuid(), club, CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task UpdateClub_AcceptsOnboardingContactColoursAndDefaultVenue()
	{
		var club = new SportsClub
		{
			Name = "Kingsbridge",
			Slug = "kingsbridge",
			SportKey = "football",
			PrimaryColor = "#123456",
			SecondaryColor = "#abcdef",
			ContactEmail = "hello@example.com",
			WebsiteUrl = "https://example.com",
			SetupStep = 2,
			Venues =
			[
				new ClubVenue
				{
					Name = "The Rec",
					Address = "Kingsbridge",
					MapUrl = "https://maps.example.com/the-rec",
					IsDefault = true
				}
			]
		};
		var controller = new OrganizationController(new StubOrganizationService(), new StubClubService());

		var result = await controller.UpdateClub(Guid.NewGuid(), club, CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
	}

	[Test]
	public async Task UpdateClub_RejectsMultipleDefaultVenues()
	{
		var club = new SportsClub
		{
			Name = "Kingsbridge",
			Slug = "kingsbridge",
			SportKey = "football",
			Venues =
			[
				new ClubVenue { Name = "One", Address = "One", IsDefault = true },
				new ClubVenue { Name = "Two", Address = "Two", IsDefault = true }
			]
		};
		var controller = new OrganizationController(new StubOrganizationService(), new StubClubService());

		var result = await controller.UpdateClub(Guid.NewGuid(), club, CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	private sealed class StubOrganizationService : IOrganizationService
	{
		public Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Organization>>([]);
		public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Organization?>(null);
		public Task<Organization?> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult<Organization?>(new());
		public Task<Organization?> CreateAsync(Organization organization, CancellationToken cancellationToken = default) => Task.FromResult<Organization?>(organization);
		public Task<Organization?> UpdateAsync(Guid id, Organization organization, CancellationToken cancellationToken = default) => Task.FromResult<Organization?>(organization);
		public Task<Organization?> UpdateCurrentAsync(Organization organization, CancellationToken cancellationToken = default) => Task.FromResult<Organization?>(organization);
		public Task<Organization?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) => Task.FromResult<Organization?>(null);
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

	private sealed class StubBillingService(bool canAddClub) : IBillingService
	{
		private readonly OrganizationSubscription _subscription = new();
		public Task<OrganizationSubscription> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult(_subscription);
		public Task<OrganizationSubscription> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default) => Task.FromResult(_subscription);
		public Task<OrganizationSubscription> UpdateCurrentAsync(SubscriptionUpdate update, CancellationToken cancellationToken = default) => Task.FromResult(_subscription);
		public Task<OrganizationSubscription> SetStatusAsync(Guid organizationId, SubscriptionStatusUpdate update, CancellationToken cancellationToken = default) => Task.FromResult(_subscription);
		public Task<IReadOnlyList<BillingInvoice>> GetInvoicesAsync(Guid organizationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BillingInvoice>>([]);
		public Task<BillingInvoice> AddInvoiceAsync(BillingInvoice invoice, CancellationToken cancellationToken = default) => Task.FromResult(invoice);
		public Task<bool> CanAddClubAsync(CancellationToken cancellationToken = default) => Task.FromResult(canAddClub);
	}
}
