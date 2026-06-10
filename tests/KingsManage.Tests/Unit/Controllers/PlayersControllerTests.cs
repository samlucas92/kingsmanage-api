using KingsManage;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public class PlayersControllerTests
{
	[Test]
	public async Task GetAll_ShouldReturnPlayers()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		playerService.Players.Add(new Player
		{
			Id = "player-1",
			Name = "Test Player",
			Number = 10,
			Positions = ["CM"],
			Appearances = 1,
			Goals = 0,
			IsActive = true
		});

		var result = await controller.GetAll(CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var players = okResult?.Value as IReadOnlyList<Player>;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(players, Is.Not.Null);
		Assert.That(players, Has.Count.EqualTo(1));
		Assert.That(players![0].Name, Is.EqualTo("Test Player"));
	}

	[Test]
	public async Task GetById_WhenPlayerExists_ShouldReturnPlayer()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		playerService.Players.Add(new Player
		{
			Id = "player-1",
			Name = "Test Player",
			Number = 10,
			Positions = ["CM"],
			IsActive = true
		});

		var result = await controller.GetById("player-1", CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var player = okResult?.Value as Player;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(player, Is.Not.Null);
		Assert.That(player!.Id, Is.EqualTo("player-1"));
		Assert.That(player.Name, Is.EqualTo("Test Player"));
	}

	[Test]
	public async Task GetById_WhenPlayerDoesNotExist_ShouldReturnNotFound()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		var result = await controller.GetById("missing-player", CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
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
	public async Task Create_WhenNameIsEmpty_ShouldReturnBadRequest()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		var result = await controller.Create(
			new Player
			{
				Name = "",
				Number = 10,
				Positions = ["CM"]
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
				Name = "Test Player",
				Number = 10,
				Positions = ["CM"],
				Appearances = 0,
				Goals = 0,
				IsActive = true
			},
			CancellationToken.None
		);

		var createdResult = result.Result as CreatedAtActionResult;
		var player = createdResult?.Value as Player;

		Assert.That(createdResult, Is.Not.Null);
		Assert.That(player, Is.Not.Null);
		Assert.That(player!.Id, Is.Not.Empty);
		Assert.That(player.Name, Is.EqualTo("Test Player"));
		Assert.That(playerService.Players, Has.Count.EqualTo(1));
	}

	[Test]
	public async Task Update_WhenPlayerDoesNotExist_ShouldReturnNotFound()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		var result = await controller.Update(
			"missing-player",
			new Player
			{
				Name = "Updated Player",
				Number = 8,
				Positions = ["CM"]
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task Update_WhenPlayerExists_ShouldReturnUpdatedPlayer()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		playerService.Players.Add(new Player
		{
			Id = "player-1",
			Name = "Old Name",
			Number = 4,
			Positions = ["CB"],
			Appearances = 2,
			Goals = 0,
			IsActive = true,
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
		});

		var result = await controller.Update(
			"player-1",
			new Player
			{
				Id = "wrong-id",
				Name = "Updated Player",
				Number = 8,
				Positions = ["CM"],
				Appearances = 3,
				Goals = 1,
				IsActive = true
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var player = okResult?.Value as Player;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(player, Is.Not.Null);
		Assert.That(player!.Id, Is.EqualTo("player-1"));
		Assert.That(player.Name, Is.EqualTo("Updated Player"));
		Assert.That(player.Number, Is.EqualTo(8));
		Assert.That(player.CreatedAt, Is.EqualTo(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
	}

	[Test]
	public async Task SetActive_WhenPlayerDoesNotExist_ShouldReturnNotFound()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		var result = await controller.SetActive(
			"missing-player",
			false,
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task SetActive_WhenPlayerExists_ShouldReturnUpdatedPlayer()
	{
		var playerService = new FakePlayerService();
		var controller = new PlayersController(playerService);

		playerService.Players.Add(new Player
		{
			Id = "player-1",
			Name = "Test Player",
			Number = 10,
			Positions = ["CM"],
			IsActive = true
		});

		var result = await controller.SetActive(
			"player-1",
			false,
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var player = okResult?.Value as Player;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(player, Is.Not.Null);
		Assert.That(player!.IsActive, Is.False);
	}

	private class FakePlayerService : IPlayerService
	{
		public List<Player> Players { get; } = [];

		public Task<IReadOnlyList<Player>> GetAllAsync(
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<Player>>(Players);
		}

		public Task<Player?> GetByIdAsync(
			string id,
			CancellationToken cancellationToken = default
		)
		{
			var player = Players.FirstOrDefault(player => player.Id == id);

			return Task.FromResult(player);
		}

		public Task<Player> CreateAsync(
			Player player,
			CancellationToken cancellationToken = default
		)
		{
			player.Id = string.IsNullOrWhiteSpace(player.Id)
				? Guid.NewGuid().ToString()
				: player.Id;

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
			var existingPlayerIndex = Players.FindIndex(
				existingPlayer => existingPlayer.Id == player.Id
			);

			if (existingPlayerIndex == -1)
			{
				return Task.FromResult<Player?>(null);
			}

			player.UpdatedAt = DateTime.UtcNow;

			Players[existingPlayerIndex] = player;

			return Task.FromResult<Player?>(player);
		}

		public Task<Player?> SetActiveAsync(
			string id,
			bool isActive,
			CancellationToken cancellationToken = default
		)
		{
			var player = Players.FirstOrDefault(player => player.Id == id);

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