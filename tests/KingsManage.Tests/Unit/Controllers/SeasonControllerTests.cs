using KingsManage;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests;

public class SeasonsControllerTests
{
	[Test]
	public async Task GetAll_ShouldReturnSeasons()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		seasonService.Seasons.Add(new Season
		{
			Id = "season-1",
			Name = "2025-2026",
			StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
			EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
			IsActive = true
		});

		var result = await controller.GetAll(CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var seasons = okResult?.Value as IReadOnlyList<Season>;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(seasons, Is.Not.Null);
		Assert.That(seasons, Has.Count.EqualTo(1));
		Assert.That(seasons![0].Name, Is.EqualTo("2025-2026"));
	}

	[Test]
	public async Task GetActive_WhenActiveSeasonExists_ShouldReturnActiveSeason()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		seasonService.Seasons.Add(new Season
		{
			Id = "season-1",
			Name = "2025-2026",
			StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
			EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
			IsActive = true
		});

		var result = await controller.GetActive(CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var season = okResult?.Value as Season;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(season, Is.Not.Null);
		Assert.That(season!.IsActive, Is.True);
	}

	[Test]
	public async Task GetActive_WhenNoActiveSeasonExists_ShouldReturnNotFound()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.GetActive(CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task GetById_WhenSeasonExists_ShouldReturnSeason()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		seasonService.Seasons.Add(new Season
		{
			Id = "season-1",
			Name = "2025-2026",
			StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
			EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
			IsActive = true
		});

		var result = await controller.GetById("season-1", CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var season = okResult?.Value as Season;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(season, Is.Not.Null);
		Assert.That(season!.Id, Is.EqualTo("season-1"));
	}

	[Test]
	public async Task GetById_WhenSeasonDoesNotExist_ShouldReturnNotFound()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.GetById("missing-season", CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task Create_WhenNameIsEmpty_ShouldReturnBadRequest()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.Create(
			new Season
			{
				Name = "",
				StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
				EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
				IsActive = true
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Create_WhenEndDateIsBeforeStartDate_ShouldReturnBadRequest()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.Create(
			new Season
			{
				Name = "2025-2026",
				StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
				EndDate = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc),
				IsActive = true
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Create_WhenSeasonIsValid_ShouldReturnCreatedSeason()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.Create(
			new Season
			{
				Name = "2025-2026",
				StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
				EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
				IsActive = true
			},
			CancellationToken.None
		);

		var createdResult = result.Result as CreatedAtActionResult;
		var season = createdResult?.Value as Season;

		Assert.That(createdResult, Is.Not.Null);
		Assert.That(season, Is.Not.Null);
		Assert.That(season!.Id, Is.Not.Empty);
		Assert.That(season.Name, Is.EqualTo("2025-2026"));
		Assert.That(seasonService.Seasons, Has.Count.EqualTo(1));
	}

	[Test]
	public async Task Update_WhenSeasonDoesNotExist_ShouldReturnNotFound()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.Update(
			"missing-season",
			new Season
			{
				Name = "Updated Season",
				StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
				EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
				IsActive = true
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task Update_WhenSeasonExists_ShouldReturnUpdatedSeason()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		seasonService.Seasons.Add(new Season
		{
			Id = "season-1",
			Name = "Old Season",
			StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
			EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
			IsActive = true,
			CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
		});

		var result = await controller.Update(
			"season-1",
			new Season
			{
				Id = "wrong-id",
				Name = "Updated Season",
				StartDate = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
				EndDate = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
				IsActive = true
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var season = okResult?.Value as Season;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(season, Is.Not.Null);
		Assert.That(season!.Id, Is.EqualTo("season-1"));
		Assert.That(season.Name, Is.EqualTo("Updated Season"));
		Assert.That(season.CreatedAt, Is.EqualTo(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
	}

	[Test]
	public async Task SetActive_WhenSeasonDoesNotExist_ShouldReturnNotFound()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.SetActive("missing-season", CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task SetActive_WhenSeasonExists_ShouldMakeOnlyThatSeasonActive()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		seasonService.Seasons.Add(new Season
		{
			Id = "season-1",
			Name = "2024-2025",
			StartDate = new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc),
			EndDate = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc),
			IsActive = true
		});

		seasonService.Seasons.Add(new Season
		{
			Id = "season-2",
			Name = "2025-2026",
			StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
			EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
			IsActive = false
		});

		var result = await controller.SetActive("season-2", CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var season = okResult?.Value as Season;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(season, Is.Not.Null);
		Assert.That(season!.Id, Is.EqualTo("season-2"));
		Assert.That(season.IsActive, Is.True);
		Assert.That(seasonService.Seasons.Single(item => item.Id == "season-1").IsActive, Is.False);
	}

	private class FakeSeasonService : ISeasonService
	{
		public List<Season> Seasons { get; } = [];

		public Task<IReadOnlyList<Season>> GetAllAsync(
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<Season>>(Seasons);
		}

		public Task<Season?> GetByIdAsync(
			string id,
			CancellationToken cancellationToken = default
		)
		{
			var season = Seasons.FirstOrDefault(season => season.Id == id);

			return Task.FromResult(season);
		}

		public Task<Season?> GetActiveAsync(
			CancellationToken cancellationToken = default
		)
		{
			var season = Seasons.FirstOrDefault(season => season.IsActive);

			return Task.FromResult(season);
		}

		public Task<Season> CreateAsync(
			Season season,
			CancellationToken cancellationToken = default
		)
		{
			season.Id = string.IsNullOrWhiteSpace(season.Id)
				? Guid.NewGuid().ToString()
				: season.Id;

			season.CreatedAt = DateTime.UtcNow;
			season.UpdatedAt = DateTime.UtcNow;

			if (season.IsActive)
			{
				foreach (var existingSeason in Seasons)
				{
					existingSeason.IsActive = false;
				}
			}

			Seasons.Add(season);

			return Task.FromResult(season);
		}

		public Task<Season?> UpdateAsync(
			Season season,
			CancellationToken cancellationToken = default
		)
		{
			var existingSeasonIndex = Seasons.FindIndex(
				existingSeason => existingSeason.Id == season.Id
			);

			if (existingSeasonIndex == -1)
			{
				return Task.FromResult<Season?>(null);
			}

			season.UpdatedAt = DateTime.UtcNow;

			if (season.IsActive)
			{
				foreach (var existingSeason in Seasons)
				{
					if (existingSeason.Id != season.Id)
					{
						existingSeason.IsActive = false;
					}
				}
			}

			Seasons[existingSeasonIndex] = season;

			return Task.FromResult<Season?>(season);
		}

		public Task<Season?> SetActiveAsync(
			string id,
			CancellationToken cancellationToken = default
		)
		{
			var season = Seasons.FirstOrDefault(season => season.Id == id);

			if (season is null)
			{
				return Task.FromResult<Season?>(null);
			}

			foreach (var existingSeason in Seasons)
			{
				existingSeason.IsActive = false;
			}

			season.IsActive = true;
			season.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Season?>(season);
		}
	}
}