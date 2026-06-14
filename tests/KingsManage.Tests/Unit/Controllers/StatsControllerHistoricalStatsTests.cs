using KingsManage;
using KingsManage.Web.Controllers;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace KingsManage.Tests.Unit.Controllers;

[TestFixture]
public class StatsControllerHistoricalStatsTests
{
	[Test]
	public async Task GetHistoricalStats_ShouldReturnStoredHistoricalStats()
	{
		var player = CreatePlayer("Player One");
		var historicalStats = new PlayerHistoricalStats
		{
			Id = Guid.NewGuid(),
			PlayerId = player.Id,
			Appearances = 12,
			Goals = 4
		};
		var controller = CreateController(
			[player],
			[historicalStats]
		);

		var result = await controller.GetHistoricalStats(CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		Assert.That(okResult, Is.Not.Null);
		var stats = okResult!.Value as List<PlayerHistoricalStats>;
		Assert.That(stats, Is.Not.Null);
		Assert.That(stats, Has.Count.EqualTo(1));
		Assert.That(stats![0].PlayerId, Is.EqualTo(player.Id));
		Assert.That(stats[0].Appearances, Is.EqualTo(12));
		Assert.That(stats[0].Goals, Is.EqualTo(4));
	}

	[Test]
	public async Task UpdateHistoricalStats_WhenPlayerIdIsInvalid_ShouldReturnBadRequest()
	{
		var controller = CreateController([], []);

		var result = await controller.UpdateHistoricalStats(
			"not-a-guid",
			new HistoricalStatsUpdateModel
			{
				Appearances = 10,
				Goals = 2
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task UpdateHistoricalStats_WhenPlayerDoesNotExist_ShouldReturnNotFound()
	{
		var controller = CreateController([], []);

		var result = await controller.UpdateHistoricalStats(
			Guid.NewGuid().ToString(),
			new HistoricalStatsUpdateModel
			{
				Appearances = 10,
				Goals = 2
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task UpdateHistoricalStats_WhenValuesAreNegative_ShouldReturnBadRequest()
	{
		var player = CreatePlayer("Player One");
		var controller = CreateController([player], []);

		var result = await controller.UpdateHistoricalStats(
			player.Id.ToString(),
			new HistoricalStatsUpdateModel
			{
				Appearances = -1,
				Goals = 2
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task UpdateHistoricalStats_WhenValuesAreValid_ShouldUpsertHistoricalStats()
	{
		var player = CreatePlayer("Player One");
		var statsService = new FakeStatsService([]);
		var controller = new StatsController(
			new FakePlayerService([player]),
			statsService
		);

		var result = await controller.UpdateHistoricalStats(
			player.Id.ToString(),
			new HistoricalStatsUpdateModel
			{
				Appearances = 15,
				Goals = 6
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		Assert.That(okResult, Is.Not.Null);
		var stats = okResult!.Value as PlayerHistoricalStats;
		Assert.That(stats, Is.Not.Null);
		Assert.That(stats!.PlayerId, Is.EqualTo(player.Id));
		Assert.That(stats.Appearances, Is.EqualTo(15));
		Assert.That(stats.Goals, Is.EqualTo(6));

		var storedStats = await statsService.GetHistoricalStatsByPlayerIdAsync(
			player.Id,
			CancellationToken.None
		);
		Assert.That(storedStats, Is.Not.Null);
		Assert.That(storedStats!.Appearances, Is.EqualTo(15));
		Assert.That(storedStats.Goals, Is.EqualTo(6));
	}

	[Test]
	public async Task UpdateHistoricalStats_WhenRecordAlreadyExists_ShouldUpdateExistingRecord()
	{
		var player = CreatePlayer("Player One");
		var existingStatsId = Guid.NewGuid();
		var createdAt = DateTime.UtcNow.AddDays(-10);
		var existingStats = new PlayerHistoricalStats
		{
			Id = existingStatsId,
			PlayerId = player.Id,
			Appearances = 5,
			Goals = 1,
			CreatedAt = createdAt
		};
		var statsService = new FakeStatsService([existingStats]);
		var controller = new StatsController(
			new FakePlayerService([player]),
			statsService
		);

		var result = await controller.UpdateHistoricalStats(
			player.Id.ToString(),
			new HistoricalStatsUpdateModel
			{
				Appearances = 20,
				Goals = 8
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		Assert.That(okResult, Is.Not.Null);
		var stats = okResult!.Value as PlayerHistoricalStats;
		Assert.That(stats, Is.Not.Null);
		Assert.That(stats!.Id, Is.EqualTo(existingStatsId));
		Assert.That(stats.CreatedAt, Is.EqualTo(createdAt));
		Assert.That(stats.Appearances, Is.EqualTo(20));
		Assert.That(stats.Goals, Is.EqualTo(8));
	}

	private static StatsController CreateController(
		List<Player> players,
		List<PlayerHistoricalStats> historicalStats
	)
	{
		return new StatsController(
			new FakePlayerService(players),
			new FakeStatsService(historicalStats)
		);
	}

	private static Player CreatePlayer(string name)
	{
		return new Player
		{
			Id = Guid.NewGuid(),
			Name = name,
			IsActive = true
		};
	}

	private sealed class FakePlayerService : IPlayerService
	{
		private readonly List<Player> _players;

		public FakePlayerService(List<Player> players)
		{
			_players = players;
		}

		public Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyList<Player>>(_players);
		}

		public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_players.FirstOrDefault(player => player.Id == id));
		}

		public Task<Player> CreateAsync(Player player, CancellationToken cancellationToken = default)
		{
			_players.Add(player);
			return Task.FromResult(player);
		}

		public Task<Player?> UpdateAsync(Player player, CancellationToken cancellationToken = default)
		{
			var index = _players.FindIndex(existingPlayer => existingPlayer.Id == player.Id);
			if (index < 0)
			{
				return Task.FromResult<Player?>(null);
			}

			_players[index] = player;
			return Task.FromResult<Player?>(player);
		}

		public Task<Player?> SetActiveAsync(
			Guid id,
			bool isActive,
			CancellationToken cancellationToken = default
		)
		{
			var player = _players.FirstOrDefault(existingPlayer => existingPlayer.Id == id);
			if (player is null)
			{
				return Task.FromResult<Player?>(null);
			}

			player.IsActive = isActive;
			return Task.FromResult<Player?>(player);
		}
	}

	private sealed class FakeStatsService : IStatsService
	{
		private readonly List<PlayerHistoricalStats> _historicalStats;

		public FakeStatsService(List<PlayerHistoricalStats> historicalStats)
		{
			_historicalStats = historicalStats;
		}

		public Task<List<PlayerSeasonStats>> GetSeasonStatsAsync(
			Guid seasonId,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(new List<PlayerSeasonStats>());
		}

		public Task<List<PlayerSeasonStats>> GetAllSeasonStatsAsync(
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(new List<PlayerSeasonStats>());
		}

		public Task<List<PlayerSeasonStats>> GetPlayerSeasonStatsAsync(
			Guid playerId,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(new List<PlayerSeasonStats>());
		}

		public Task<List<PlayerHistoricalStats>> GetHistoricalStatsAsync(
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(_historicalStats);
		}

		public Task<PlayerHistoricalStats?> GetHistoricalStatsByPlayerIdAsync(
			Guid playerId,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(_historicalStats.FirstOrDefault(stats => stats.PlayerId == playerId));
		}

		public Task<PlayerHistoricalStats> UpsertHistoricalStatsAsync(
			PlayerHistoricalStats stats,
			CancellationToken cancellationToken = default
		)
		{
			var existingStats = _historicalStats.FirstOrDefault(
				existingStats => existingStats.PlayerId == stats.PlayerId
			);

			if (existingStats is not null)
			{
				existingStats.Appearances = stats.Appearances;
				existingStats.Goals = stats.Goals;
				existingStats.UpdatedAt = DateTime.UtcNow;

				return Task.FromResult(existingStats);
			}

			stats.Id = stats.Id == Guid.Empty ? Guid.NewGuid() : stats.Id;
			stats.CreatedAt = DateTime.UtcNow;
			stats.UpdatedAt = DateTime.UtcNow;
			_historicalStats.Add(stats);

			return Task.FromResult(stats);
		}

		public Task RecalculateSeasonStatsAsync(
			Guid seasonId,
			CancellationToken cancellationToken = default
		)
		{
			return Task.CompletedTask;
		}
	}
}
