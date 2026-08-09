using KingsManage;
using KingsManage.Web.Controllers;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

[TestFixture]
public sealed class PlatformOrganizationsControllerTests
{
	[Test]
	public async Task GetAll_IncludesEveryOrganizationAdministratorAccount()
	{
		var service = new StubOrganizationService();
		var organization = await service.CreateAsync(
			new Organization { Name = "South Coast Rugby", Slug = "south-coast-rugby" });
		service.AdministratorAccounts.Add(new OrganizationAdministratorAccount
		{
			OrganizationId = organization!.Id,
			UserId = Guid.NewGuid(),
			Email = "admin@south-coast.test",
			IsActive = true
		});
		var controller = new PlatformOrganizationsController(service, new StubOnboardingService());

		var result = await controller.GetAll(CancellationToken.None);

		var ok = result.Result as OkObjectResult;
		var organizations = ok?.Value as IReadOnlyList<PlatformOrganizationViewModel>;
		Assert.That(organizations, Has.Count.EqualTo(1));
		Assert.That(organizations![0].Administrators.Single().Email, Is.EqualTo("admin@south-coast.test"));
	}

	[Test]
	public async Task Create_ReturnsCreatedOrganization()
	{
		var service = new StubOrganizationService();
		var controller = new PlatformOrganizationsController(service, new StubOnboardingService());

		var result = await controller.Create(
			new Organization { Name = "South Coast Rugby", Slug = "south-coast-rugby" },
			CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<CreatedResult>());
		Assert.That(service.Organizations, Has.Count.EqualTo(1));
	}

	[Test]
	public async Task SetActive_ArchivesAnOrganization()
	{
		var service = new StubOrganizationService();
		var organization = await service.CreateAsync(
			new Organization { Name = "South Coast Rugby", Slug = "south-coast-rugby" });
		var controller = new PlatformOrganizationsController(service, new StubOnboardingService());

		var result = await controller.SetActive(
			organization!.Id,
			new PlatformOrganizationsController.SetActiveRequest { IsActive = false },
			CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
		Assert.That(service.Organizations.Single().IsActive, Is.False);
	}

	[Test]
	public async Task Delete_RemovesAnOrganization()
	{
		var service = new StubOrganizationService();
		var organization = await service.CreateAsync(
			new Organization { Name = "Unused", Slug = "unused" });
		var controller = new PlatformOrganizationsController(service, new StubOnboardingService());

		var result = await controller.Delete(
			organization!.Id,
			CancellationToken.None);

		Assert.That(result, Is.TypeOf<NoContentResult>());
		Assert.That(service.Organizations, Is.Empty);
	}

	[Test]
	public async Task Onboard_WithCompleteWorkspace_ReturnsCreatedResult()
	{
		var onboarding = new StubOnboardingService();
		var controller = new PlatformOrganizationsController(
			new StubOrganizationService(),
			onboarding);

		var result = await controller.Onboard(ValidOnboardingInput(), CancellationToken.None);

		Assert.Multiple(() =>
		{
			Assert.That(result.Result, Is.TypeOf<CreatedResult>());
			Assert.That(onboarding.LastInput?.AdministratorEmail, Is.EqualTo("admin@harbour.test"));
			Assert.That(onboarding.LastInput?.ClubAllowance, Is.EqualTo(2));
		});
	}

	[Test]
	public async Task Onboard_WithWeakTemporaryPassword_ReturnsBadRequest()
	{
		var onboarding = new StubOnboardingService();
		var controller = new PlatformOrganizationsController(
			new StubOrganizationService(),
			onboarding);
		var input = ValidOnboardingInput();
		input.TemporaryPassword = "short";

		var result = await controller.Onboard(input, CancellationToken.None);

		Assert.Multiple(() =>
		{
			Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
			Assert.That(onboarding.LastInput, Is.Null);
		});
	}

	private static PlatformOrganizationOnboardingInput ValidOnboardingInput() => new()
	{
		OrganizationName = "Harbour Sports",
		OrganizationSlug = "harbour-sports",
		ClubName = "Harbour FC",
		ClubSlug = "harbour-fc",
		SportKey = "football",
		PrimaryColor = "#0f766e",
		SecondaryColor = "#d9f99d",
		AdministratorEmail = "admin@harbour.test",
		TemporaryPassword = "Temporary123!",
		ClubAllowance = 2,
		BillingEmail = "billing@harbour.test",
		SubscriptionStatus = SubscriptionStatus.Trialing
	};

	private sealed class StubOnboardingService : IPlatformOrganizationOnboardingService
	{
		public PlatformOrganizationOnboardingInput? LastInput { get; private set; }

		public Task<PlatformOrganizationOnboardingOutcome> CreateAsync(
			PlatformOrganizationOnboardingInput input,
			CancellationToken cancellationToken = default)
		{
			LastInput = input;
			var organization = new Organization
			{
				Id = Guid.NewGuid(),
				Name = input.OrganizationName,
				Slug = input.OrganizationSlug,
				IsActive = true
			};
			return Task.FromResult(new PlatformOrganizationOnboardingOutcome(
				PlatformOrganizationOnboardingStatus.Created,
				new PlatformOrganizationOnboardingResult
				{
					Organization = organization,
					Club = new SportsClub
					{
						Id = Guid.NewGuid(),
						OrganizationId = organization.Id,
						Name = input.ClubName,
						Slug = input.ClubSlug,
						SportKey = input.SportKey
					},
					AdministratorEmail = input.AdministratorEmail,
					Subscription = new OrganizationSubscription
					{
						OrganizationId = organization.Id,
						ClubAllowance = input.ClubAllowance
					}
				}));
		}
	}

	private sealed class StubOrganizationService : IOrganizationService
	{
		public List<Organization> Organizations { get; } = [];
		public List<OrganizationAdministratorAccount> AdministratorAccounts { get; } = [];

		public Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<Organization>>(Organizations);
		public Task<IReadOnlyList<OrganizationAdministratorAccount>> GetAdministratorAccountsAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<OrganizationAdministratorAccount>>(AdministratorAccounts);
		public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult(Organizations.FirstOrDefault(item => item.Id == id));
		public Task<Organization?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<Organization?>(Organizations.FirstOrDefault());
		public Task<Organization?> CreateAsync(Organization organization, CancellationToken cancellationToken = default)
		{
			organization.Id = Guid.NewGuid();
			organization.IsActive = true;
			Organizations.Add(organization);
			return Task.FromResult<Organization?>(organization);
		}
		public Task<Organization?> UpdateAsync(Guid id, Organization organization, CancellationToken cancellationToken = default) =>
			Task.FromResult<Organization?>(organization);
		public Task<Organization?> UpdateCurrentAsync(Organization organization, CancellationToken cancellationToken = default) =>
			Task.FromResult<Organization?>(organization);
		public Task<Organization?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
		{
			var organization = Organizations.FirstOrDefault(item => item.Id == id);
			if (organization is not null) organization.IsActive = isActive;
			return Task.FromResult<Organization?>(organization);
		}
		public Task<OrganizationDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var removed = Organizations.RemoveAll(item => item.Id == id);
			return Task.FromResult(removed > 0
				? OrganizationDeleteResult.Deleted
				: OrganizationDeleteResult.NotFound);
		}
	}
}
