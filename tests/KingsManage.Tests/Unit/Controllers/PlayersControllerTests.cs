using KingsManage;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public class PlayersControllerTests
{
	private static readonly Guid PlayerOneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid PlayerTwoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
	private static readonly Guid MissingPlayerId = Guid.Parse("99999999-9999-9999-9999-999999999999");

	[Test]
	public async Task GetAll_ShouldReturnAllPlayers()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		playerService.Players.Add(CreatePlayer(PlayerOneId, "Player One"));
		playerService.Players.Add(CreatePlayer(PlayerTwoId, "Player Two"));

		var result = await controller.GetAll(CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var players = okResult?.Value as IReadOnlyList<Player>;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(players, Is.Not.Null);
		Assert.That(players, Has.Count.EqualTo(2));
	}

	[Test]
	public async Task GetById_WhenIdIsEmpty_ShouldReturnBadRequest()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		var result = await controller.GetById("", CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task GetById_WhenIdIsNotGuid_ShouldReturnBadRequest()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		var result = await controller.GetById("not-a-guid", CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task GetById_WhenPlayerDoesNotExist_ShouldReturnNotFound()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		var result = await controller.GetById(MissingPlayerId.ToString(), CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task GetById_WhenPlayerExists_ShouldReturnPlayer()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		playerService.Players.Add(CreatePlayer(PlayerOneId, "Player One"));

		var result = await controller.GetById(PlayerOneId.ToString(), CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var player = okResult?.Value as Player;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(player, Is.Not.Null);
		Assert.That(player!.Id, Is.EqualTo(PlayerOneId));
		Assert.That(player.Name, Is.EqualTo("Player One"));
	}

	[Test]
	public async Task Create_WhenNameIsEmpty_ShouldReturnBadRequest()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		var result = await controller.Create(
			new Player
			{
				Name = ""
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Create_WhenPlayerIsValid_ShouldReturnCreatedPlayer()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		var result = await controller.Create(
			new Player
			{
				Name = "New Player",
				Number = 9,
				Positions = ["ST"],
				Appearances = 10,
				Goals = 4,
				IsActive = true
			},
			CancellationToken.None
		);

		var createdResult = result.Result as CreatedAtActionResult;
		var player = createdResult?.Value as Player;

		Assert.That(createdResult, Is.Not.Null);
		Assert.That(player, Is.Not.Null);
		Assert.That(player!.Id, Is.Not.EqualTo(Guid.Empty));
		Assert.That(player.Name, Is.EqualTo("New Player"));
		Assert.That(playerService.Players, Has.Count.EqualTo(1));
	}

	[Test]
	public async Task Update_WhenIdIsNotGuid_ShouldReturnBadRequest()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		var result = await controller.Update(
			"not-a-guid",
			CreatePlayer(PlayerOneId, "Updated Player"),
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Update_WhenPlayerDoesNotExist_ShouldReturnNotFound()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		var result = await controller.Update(
			MissingPlayerId.ToString(),
			CreatePlayer(Guid.Empty, "Updated Player"),
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task Update_WhenPlayerExists_ShouldReturnUpdatedPlayer()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);
		var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		playerService.Players.Add(new Player
		{
			Id = PlayerOneId,
			Name = "Old Player",
			Number = 4,
			Positions = ["CB"],
			IsActive = true,
			CreatedAt = createdAt,
			UpdatedAt = createdAt
		});

		var result = await controller.Update(
			PlayerOneId.ToString(),
			new Player
			{
				Id = Guid.NewGuid(),
				Name = "Updated Player",
				Number = 8,
				Positions = ["CM"],
				Appearances = 12,
				Goals = 2,
				IsActive = true
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var player = okResult?.Value as Player;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(player, Is.Not.Null);
		Assert.That(player!.Id, Is.EqualTo(PlayerOneId));
		Assert.That(player.Name, Is.EqualTo("Updated Player"));
		Assert.That(player.CreatedAt, Is.EqualTo(createdAt));
	}

	[Test]
	public async Task SetActive_WhenPlayerExists_ShouldReturnUpdatedPlayer()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		playerService.Players.Add(CreatePlayer(PlayerOneId, "Player One"));

		var result = await controller.SetActive(
			PlayerOneId.ToString(),
			false,
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var player = okResult?.Value as Player;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(player, Is.Not.Null);
		Assert.That(player!.IsActive, Is.False);
	}

	private static Player CreatePlayer(Guid id, string name)
	{
		return new Player
		{
			Id = id,
			Name = name,
			Number = 1,
			Positions = ["CM"],
			Appearances = 0,
			Goals = 0,
			IsActive = true,
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
		};
	}

	private class FakePlayerService : IPlayerService
	{
		public List<Player> Players { get; } = [];

		public Task<IReadOnlyList<Player>> GetAllAsync(
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<Player>>(
				Players.OrderBy(player => player.Name).ToList()
			);
		}

		public Task<Player?> GetByIdAsync(
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(
				Players.FirstOrDefault(player => player.Id == id)
			);
		}

		public Task<Player> CreateAsync(
			Player player,
			CancellationToken cancellationToken = default
		)
		{
			player.Id = player.Id == Guid.Empty
				? Guid.NewGuid()
				: player.Id;

			player.Name = player.Name.Trim();
			player.CreatedAt = DateTime.UtcNow;
			player.UpdatedAt = DateTime.UtcNow;
			Players.Add(player);

			return Task.FromResult(player);
		}

		public Task<Player?> UpdateAsync(
			Player player,
			CancellationToken cancellationToken = default
		)
		{
			var index = Players.FindIndex(
				existingPlayer => existingPlayer.Id == player.Id
			);

			if (index == -1)
			{
				return Task.FromResult<Player?>(null);
			}

			player.Name = player.Name.Trim();
			player.UpdatedAt = DateTime.UtcNow;
			Players[index] = player;

			return Task.FromResult<Player?>(player);
		}

		public Task<Player?> SetActiveAsync(
			Guid id,
			bool isActive,
			CancellationToken cancellationToken = default
		)
		{
			var player = Players.FirstOrDefault(
				currentPlayer => currentPlayer.Id == id
			);

			if (player is null)
			{
				return Task.FromResult<Player?>(null);
			}

			player.IsActive = isActive;
			player.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Player?>(player);
		}
	}
}
