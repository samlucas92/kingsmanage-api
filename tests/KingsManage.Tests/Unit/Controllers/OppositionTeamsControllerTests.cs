using KingsManage;
using KingsManage.Web.Controllers;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public sealed class OppositionTeamsControllerTests
{
	[Test]
	public void ControllerRequiresTeamManagementAndWritesRequireClubAdmin()
	{
		var controllerPolicy = typeof(OppositionTeamsController)
			.GetCustomAttributes(typeof(AuthorizeAttribute), true)
			.Cast<AuthorizeAttribute>()
			.Single()
			.Policy;

		Assert.That(controllerPolicy, Is.EqualTo("TeamManagement"));
		foreach (var methodName in new[] { "Create", "Update" })
		{
			var policy = typeof(OppositionTeamsController)
				.GetMethod(methodName)!
				.GetCustomAttributes(typeof(AuthorizeAttribute), true)
				.Cast<AuthorizeAttribute>()
				.Single()
				.Policy;
			Assert.That(policy, Is.EqualTo("ClubAdmin"));
		}
	}

	[Test]
	public async Task CreateTrimsAndReturnsTheSavedTeam()
	{
		var service = new FakeOppositionTeamService();
		var controller = new OppositionTeamsController(service);

		var result = await controller.Create(new SaveOppositionTeamModel
		{
			Name = "  Riverside Athletic  ",
			Location = "  Riverside Ground  ",
			IsActive = true
		}, CancellationToken.None);

		var created = result.Result as CreatedAtActionResult;
		var team = created?.Value as OppositionTeam;
		Assert.Multiple(() =>
		{
			Assert.That(team?.Name, Is.EqualTo("Riverside Athletic"));
			Assert.That(team?.Location, Is.EqualTo("Riverside Ground"));
			Assert.That(team?.Id, Is.Not.EqualTo(Guid.Empty));
		});
	}

	[Test]
	public async Task CreateRejectsMissingNamesWithoutCallingTheService()
	{
		var service = new FakeOppositionTeamService();
		var controller = new OppositionTeamsController(service);

		var result = await controller.Create(
			new SaveOppositionTeamModel { Name = "   " },
			CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
		Assert.That(service.Teams, Is.Empty);
	}

	[Test]
	public async Task CreateReturnsConflictForDuplicateNames()
	{
		var controller = new OppositionTeamsController(
			new FakeOppositionTeamService { RejectCreatesAsDuplicates = true });

		var result = await controller.Create(
			new SaveOppositionTeamModel { Name = "Riverside Athletic" },
			CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<ConflictObjectResult>());
	}

	private sealed class FakeOppositionTeamService : IOppositionTeamService
	{
		public List<OppositionTeam> Teams { get; } = [];
		public bool RejectCreatesAsDuplicates { get; init; }

		public Task<IReadOnlyList<OppositionTeam>> GetAllAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<OppositionTeam>>(Teams);

		public Task<OppositionTeam?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult(Teams.SingleOrDefault(team => team.Id == id));

		public Task<OppositionTeam> CreateAsync(OppositionTeam team, CancellationToken cancellationToken = default)
		{
			if (RejectCreatesAsDuplicates) throw new ArgumentException("An opposition team with this name already exists.");
			team.Id = Guid.NewGuid();
			team.Name = team.Name.Trim();
			team.Location = team.Location.Trim();
			Teams.Add(team);
			return Task.FromResult(team);
		}

		public Task<OppositionTeam?> UpdateAsync(OppositionTeam team, CancellationToken cancellationToken = default) =>
			Task.FromResult<OppositionTeam?>(team);

		public Task<OppositionTeam?> SetBadgeFileAsync(Guid id, Guid? badgeFileId, CancellationToken cancellationToken = default) =>
			Task.FromResult<OppositionTeam?>(Teams.SingleOrDefault(team => team.Id == id));
	}
}
