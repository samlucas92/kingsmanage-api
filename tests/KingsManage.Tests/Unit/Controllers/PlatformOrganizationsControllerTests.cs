using KingsManage;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

[TestFixture]
public sealed class PlatformOrganizationsControllerTests
{
	[Test]
	public async Task Create_ReturnsCreatedOrganization()
	{
		var service = new StubOrganizationService();
		var controller = new PlatformOrganizationsController(service);

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
		var controller = new PlatformOrganizationsController(service);

		var result = await controller.SetActive(
			organization!.Id,
			new PlatformOrganizationsController.SetActiveRequest { IsActive = false },
			CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
		Assert.That(service.Organizations.Single().IsActive, Is.False);
	}

	private sealed class StubOrganizationService : IOrganizationService
	{
		public List<Organization> Organizations { get; } = [];

		public Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<Organization>>(Organizations);
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
	}
}
