using KingsManage;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public sealed class OrganizationLocationsControllerTests
{
	[Test]
	public void ControllerRequiresTeamManagementAndWritesRequireClubAdmin()
	{
		var controllerPolicy = typeof(OrganizationLocationsController)
			.GetCustomAttributes(typeof(AuthorizeAttribute), true)
			.Cast<AuthorizeAttribute>()
			.Single()
			.Policy;
		var writeMethods = new[] { "Create", "Update", "Delete" };

		Assert.That(controllerPolicy, Is.EqualTo("TeamManagement"));
		foreach (var methodName in writeMethods)
		{
			var policy = typeof(OrganizationLocationsController)
				.GetMethod(methodName)!
				.GetCustomAttributes(typeof(AuthorizeAttribute), true)
				.Cast<AuthorizeAttribute>()
				.Single()
				.Policy;
			Assert.That(policy, Is.EqualTo("ClubAdmin"));
		}
	}

	[Test]
	public async Task GetAllReturnsKnownLocations()
	{
		var service = new FakeOrganizationLocationService();
		service.Locations.Add(CreateLocation("The Hut"));
		var controller = new OrganizationLocationsController(service);

		var result = await controller.GetAll(CancellationToken.None);

		var ok = result.Result as OkObjectResult;
		var locations = ok?.Value as IReadOnlyList<OrganizationLocation>;
		Assert.That(locations, Has.Count.EqualTo(1));
		Assert.That(locations![0].Name, Is.EqualTo("The Hut"));
	}

	[TestCase("", "Club Road", "Location name is required.")]
	[TestCase("The Hut", "", "Address is required.")]
	public async Task CreateRejectsIncompleteLocations(
		string name,
		string address,
		string expectedMessage
	)
	{
		var service = new FakeOrganizationLocationService();
		var controller = new OrganizationLocationsController(service);

		var result = await controller.Create(
			new OrganizationLocation { Name = name, Address = address },
			CancellationToken.None
		);

		var badRequest = result.Result as BadRequestObjectResult;
		Assert.That(badRequest?.Value, Is.EqualTo(expectedMessage));
		Assert.That(service.CreateCalls, Is.Zero);
	}

	[Test]
	public async Task CreateReturnsTheSavedLocation()
	{
		var service = new FakeOrganizationLocationService();
		var controller = new OrganizationLocationsController(service);

		var result = await controller.Create(
			CreateLocation("The Hut"),
			CancellationToken.None
		);

		var created = result.Result as CreatedResult;
		var location = created?.Value as OrganizationLocation;
		Assert.That(location?.Id, Is.Not.EqualTo(Guid.Empty));
		Assert.That(service.CreateCalls, Is.EqualTo(1));
	}

	[Test]
	public async Task DeleteUnknownLocationReturnsNotFound()
	{
		var controller = new OrganizationLocationsController(
			new FakeOrganizationLocationService()
		);

		var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

		Assert.That(result, Is.TypeOf<NotFoundResult>());
	}

	private static OrganizationLocation CreateLocation(string name)
	{
		return new OrganizationLocation
		{
			Name = name,
			Address = "123 Club Road, Kingsbridge"
		};
	}

	private sealed class FakeOrganizationLocationService : IOrganizationLocationService
	{
		public List<OrganizationLocation> Locations { get; } = [];
		public int CreateCalls { get; private set; }

		public Task<IReadOnlyList<OrganizationLocation>> GetAllAsync(
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<OrganizationLocation>>(Locations);
		}

		public Task<OrganizationLocation?> GetByIdAsync(
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(Locations.SingleOrDefault(location => location.Id == id));
		}

		public Task<OrganizationLocation> CreateAsync(
			OrganizationLocation location,
			CancellationToken cancellationToken = default
		)
		{
			CreateCalls++;
			location.Id = Guid.NewGuid();
			Locations.Add(location);
			return Task.FromResult(location);
		}

		public Task<OrganizationLocation?> UpdateAsync(
			OrganizationLocation location,
			CancellationToken cancellationToken = default
		)
		{
			var index = Locations.FindIndex(item => item.Id == location.Id);
			if (index < 0)
			{
				return Task.FromResult<OrganizationLocation?>(null);
			}
			Locations[index] = location;
			return Task.FromResult<OrganizationLocation?>(location);
		}

		public Task<bool> DeleteAsync(
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(Locations.RemoveAll(location => location.Id == id) > 0);
		}
	}
}
