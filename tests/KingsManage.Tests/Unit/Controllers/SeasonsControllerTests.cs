using KingsManage;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public class SeasonsControllerTests
{
	private static readonly Guid SeasonOneId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
	private static readonly Guid SeasonTwoId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
	private static readonly Guid MissingSeasonId = Guid.Parse("99999999-9999-9999-9999-999999999999");

	[Test]
	public async Task GetAll_ShouldReturnAllSeasons()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		seasonService.Seasons.Add(CreateSeason(SeasonOneId, "2025-2026", true));
		seasonService.Seasons.Add(CreateSeason(SeasonTwoId, "2026-2027", false));

		var result = await controller.GetAll(CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var seasons = okResult?.Value as IReadOnlyList<Season>;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(seasons, Is.Not.Null);
		Assert.That(seasons, Has.Count.EqualTo(2));
	}

	[Test]
	public async Task GetActive_WhenActiveSeasonExists_ShouldReturnActiveSeason()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		seasonService.Seasons.Add(CreateSeason(SeasonOneId, "2025-2026", true));
		seasonService.Seasons.Add(CreateSeason(SeasonTwoId, "2026-2027", false));

		var result = await controller.GetActive(CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var season = okResult?.Value as Season;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(season, Is.Not.Null);
		Assert.That(season!.Id, Is.EqualTo(SeasonOneId));
		Assert.That(season.IsActive, Is.True);
	}

	[Test]
	public async Task GetActive_WhenNoActiveSeasonExists_ShouldReturnNotFound()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		seasonService.Seasons.Add(CreateSeason(SeasonOneId, "2025-2026", false));

		var result = await controller.GetActive(CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task GetById_WhenIdIsEmpty_ShouldReturnBadRequest()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.GetById("", CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task GetById_WhenIdIsNotGuid_ShouldReturnBadRequest()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.GetById("not-a-guid", CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task GetById_WhenSeasonDoesNotExist_ShouldReturnNotFound()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.GetById(MissingSeasonId.ToString(), CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task GetById_WhenSeasonExists_ShouldReturnSeason()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		seasonService.Seasons.Add(CreateSeason(SeasonOneId, "2025-2026", true));

		var result = await controller.GetById(SeasonOneId.ToString(), CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var season = okResult?.Value as Season;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(season, Is.Not.Null);
		Assert.That(season!.Id, Is.EqualTo(SeasonOneId));
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
				EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc)
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
		Assert.That(season!.Id, Is.Not.EqualTo(Guid.Empty));
		Assert.That(season.IsActive, Is.True);
		Assert.That(seasonService.Seasons, Has.Count.EqualTo(1));
	}

	[Test]
	public async Task Update_WhenIdIsNotGuid_ShouldReturnBadRequest()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.Update(
			"not-a-guid",
			CreateSeason(SeasonOneId, "2025-2026", true),
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Update_WhenSeasonDoesNotExist_ShouldReturnNotFound()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.Update(
			MissingSeasonId.ToString(),
			CreateSeason(Guid.Empty, "2025-2026", true),
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task Update_WhenSeasonExists_ShouldReturnUpdatedSeason()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);
		var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		seasonService.Seasons.Add(new Season
		{
			Id = SeasonOneId,
			Name = "Old Season",
			StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
			EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
			IsActive = true,
			CreatedAt = createdAt,
			UpdatedAt = createdAt
		});

		var result = await controller.Update(
			SeasonOneId.ToString(),
			new Season
			{
				Id = Guid.NewGuid(),
				Name = "Updated Season",
				StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
				EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
				IsActive = true
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var season = okResult?.Value as Season;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(season, Is.Not.Null);
		Assert.That(season!.Id, Is.EqualTo(SeasonOneId));
		Assert.That(season.Name, Is.EqualTo("Updated Season"));
		Assert.That(season.CreatedAt, Is.EqualTo(createdAt));
	}

	[Test]
	public async Task SetActive_WhenSeasonExists_ShouldSetOnlyThatSeasonActive()
	{
		var seasonService = new FakeSeasonService();
		var controller = new SeasonsController(seasonService);

		seasonService.Seasons.Add(CreateSeason(SeasonOneId, "2025-2026", true));
		seasonService.Seasons.Add(CreateSeason(SeasonTwoId, "2026-2027", false));

		var result = await controller.SetActive(SeasonTwoId.ToString(), CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var season = okResult?.Value as Season;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(season, Is.Not.Null);
		Assert.That(season!.Id, Is.EqualTo(SeasonTwoId));
		Assert.That(season.IsActive, Is.True);
		Assert.That(seasonService.Seasons.Single(currentSeason => currentSeason.Id == SeasonOneId).IsActive, Is.False);
	}

	private static Season CreateSeason(Guid id, string name, bool isActive)
	{
		return new Season
		{
			Id = id,
			Name = name,
			StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
			EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
			IsActive = isActive,
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
		};
	}

	private class FakeSeasonService : ISeasonService
	{
		public List<Season> Seasons { get; } = [];

		public Task<IReadOnlyList<Season>> GetAllAsync(
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<Season>>(
				Seasons.OrderByDescending(season => season.StartDate).ToList()
			);
		}

		public Task<Season?> GetByIdAsync(
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(
				Seasons.FirstOrDefault(season => season.Id == id)
			);
		}

		public Task<Season?> GetActiveAsync(
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(
				Seasons.FirstOrDefault(season => season.IsActive)
			);
		}

		public Task<Season> CreateAsync(
			Season season,
			CancellationToken cancellationToken = default
		)
		{
			season.Id = season.Id == Guid.Empty
				? Guid.NewGuid()
				: season.Id;

			season.Name = season.Name.Trim();
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
			var index = Seasons.FindIndex(
				existingSeason => existingSeason.Id == season.Id
			);

			if (index == -1)
			{
				return Task.FromResult<Season?>(null);
			}

			season.Name = season.Name.Trim();
			season.UpdatedAt = DateTime.UtcNow;

			if (season.IsActive)
			{
				foreach (var existingSeason in Seasons)
				{
					existingSeason.IsActive = false;
				}
			}

			Seasons[index] = season;

			return Task.FromResult<Season?>(season);
		}

		public Task<Season?> SetActiveAsync(
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			var season = Seasons.FirstOrDefault(
				currentSeason => currentSeason.Id == id
			);

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
