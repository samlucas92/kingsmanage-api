using KingsManage;
using KingsManage.Web;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public class MatchesControllerTests
{
	[Test]
	public async Task GetAll_WhenNoSeasonIdProvided_ShouldReturnAllMatches()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch("match-1", "season-1", "Team One"));
		matchService.Matches.Add(CreateMatch("match-2", "season-2", "Team Two"));

		var result = await controller.GetAll(null, CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var matches = okResult?.Value as IReadOnlyList<Match>;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(matches, Is.Not.Null);
		Assert.That(matches, Has.Count.EqualTo(2));
	}

	[Test]
	public async Task GetAll_WhenSeasonIdProvided_ShouldReturnSeasonMatches()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch("match-1", "season-1", "Team One"));
		matchService.Matches.Add(CreateMatch("match-2", "season-2", "Team Two"));

		var result = await controller.GetAll("season-1", CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var matches = okResult?.Value as IReadOnlyList<Match>;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(matches, Is.Not.Null);
		Assert.That(matches, Has.Count.EqualTo(1));
		Assert.That(matches![0].SeasonId, Is.EqualTo("season-1"));
	}

	[Test]
	public async Task GetById_WhenMatchExists_ShouldReturnMatch()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch("match-1", "season-1", "Test Opponent"));

		var result = await controller.GetById("match-1", CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var match = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.Id, Is.EqualTo("match-1"));
		Assert.That(match.Opponent, Is.EqualTo("Test Opponent"));
	}

	[Test]
	public async Task GetById_WhenMatchDoesNotExist_ShouldReturnNotFound()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.GetById("missing-match", CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task GetById_WhenIdIsEmpty_ShouldReturnBadRequest()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.GetById("", CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Create_WhenOpponentIsEmpty_ShouldReturnBadRequest()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.Create(
			new Match
			{
				SeasonId = "season-1",
				Team = ClubTeam.First,
				Opponent = "",
				Date = new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc),
				Venue = MatchVenue.Home,
				SelectedFormation = LineupFormation.FourThreeThree
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Create_WhenDateIsDefault_ShouldReturnBadRequest()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.Create(
			new Match
			{
				SeasonId = "season-1",
				Team = ClubTeam.First,
				Opponent = "Test Opponent",
				Date = default,
				Venue = MatchVenue.Home,
				SelectedFormation = LineupFormation.FourThreeThree
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Create_WhenMatchIsValid_ShouldReturnCreatedMatch()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.Create(
			new Match
			{
				SeasonId = "season-1",
				Team = ClubTeam.First,
				Opponent = "Test Opponent",
				Date = new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc),
				Venue = MatchVenue.Home,
				SelectedFormation = LineupFormation.FourThreeThree
			},
			CancellationToken.None
		);

		var createdResult = result.Result as CreatedAtActionResult;
		var match = createdResult?.Value as Match;

		Assert.That(createdResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.Id, Is.Not.Empty);
		Assert.That(match.Opponent, Is.EqualTo("Test Opponent"));
		Assert.That(match.State, Is.EqualTo(MatchState.Upcoming));
		Assert.That(matchService.Matches, Has.Count.EqualTo(1));
	}

	[Test]
	public async Task Update_WhenMatchDoesNotExist_ShouldReturnNotFound()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.Update(
			"missing-match",
			CreateMatch("wrong-id", "season-1", "Updated Opponent"),
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task Update_WhenMatchExists_ShouldReturnUpdatedMatch()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		matchService.Matches.Add(new Match
		{
			Id = "match-1",
			SeasonId = "season-1",
			Team = ClubTeam.First,
			Opponent = "Old Opponent",
			Date = new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc),
			Venue = MatchVenue.Home,
			State = MatchState.Upcoming,
			SelectedFormation = LineupFormation.FourThreeThree,
			CreatedAt = createdAt
		});

		var result = await controller.Update(
			"match-1",
			new Match
			{
				Id = "wrong-id",
				SeasonId = "season-1",
				Team = ClubTeam.Second,
				Opponent = "Updated Opponent",
				Date = new DateTime(2026, 8, 8, 14, 0, 0, DateTimeKind.Utc),
				Venue = MatchVenue.Away,
				State = MatchState.Upcoming,
				SelectedFormation = LineupFormation.FourTwoThreeOne
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var match = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.Id, Is.EqualTo("match-1"));
		Assert.That(match.Opponent, Is.EqualTo("Updated Opponent"));
		Assert.That(match.Team, Is.EqualTo(ClubTeam.Second));
		Assert.That(match.CreatedAt, Is.EqualTo(createdAt));
	}

	[Test]
	public async Task Delete_WhenIdIsEmpty_ShouldReturnBadRequest()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.Delete("", CancellationToken.None);

		Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Delete_WhenMatchDoesNotExist_ShouldReturnNotFound()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.Delete("missing-match", CancellationToken.None);

		Assert.That(result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task Delete_WhenMatchExists_ShouldReturnNoContent()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch("match-1", "season-1", "Test Opponent"));

		var result = await controller.Delete("match-1", CancellationToken.None);

		Assert.That(result, Is.TypeOf<NoContentResult>());
		Assert.That(matchService.Matches, Is.Empty);
	}

	[Test]
	public async Task SetLineup_WhenMatchExists_ShouldReturnUpdatedMatch()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch("match-1", "season-1", "Test Opponent"));

		var selectedPlayers = new List<SelectedPlayer>
		{
			new()
			{
				PlayerId = "player-1",
				X = 50,
				Y = 60,
				Area = "pitch",
				PositionIndex = 1
			}
		};

		var result = await controller.SetLineup("match-1", selectedPlayers, CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var match = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.SelectedPlayers, Has.Count.EqualTo(1));
		Assert.That(match.SelectedPlayers[0].PlayerId, Is.EqualTo("player-1"));
	}

	[Test]
	public async Task SetLineup_WhenMatchDoesNotExist_ShouldReturnNotFound()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.SetLineup(
			"missing-match",
			[],
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task SetFormation_WhenMatchExists_ShouldReturnUpdatedFormation()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch("match-1", "season-1", "Test Opponent"));

		var result = await controller.SetFormation(
			"match-1",
			LineupFormation.FourTwoThreeOne,
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var match = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.SelectedFormation, Is.EqualTo(LineupFormation.FourTwoThreeOne));
	}

	[Test]
	public async Task ToggleLineupLocked_WhenMatchExists_ShouldToggleLineupLock()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch("match-1", "season-1", "Test Opponent"));

		var result = await controller.ToggleLineupLocked("match-1", CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var match = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.IsLineupLocked, Is.True);
	}

	[Test]
	public async Task SetResult_WhenGoalsAreNegative_ShouldReturnBadRequest()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.SetResult(
			"match-1",
			new MatchResult
			{
				HomeGoals = -1,
				AwayGoals = 0
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task SetResult_WhenHomeMatchWon_ShouldReturnCompletedWonMatch()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch("match-1", "season-1", "Test Opponent"));

		var result = await controller.SetResult(
			"match-1",
			new MatchResult
			{
				HomeGoals = 3,
				AwayGoals = 1
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var match = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.IsCompleted, Is.True);
		Assert.That(match.State, Is.EqualTo(MatchState.Won));
		Assert.That(match.Result!.HomeGoals, Is.EqualTo(3));
	}

	[Test]
	public async Task SetResult_WhenAwayMatchHomeTeamWins_ShouldReturnLostMatch()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var match = CreateMatch("match-1", "season-1", "Test Opponent");
		match.Venue = MatchVenue.Away;

		matchService.Matches.Add(match);

		var result = await controller.SetResult(
			"match-1",
			new MatchResult
			{
				HomeGoals = 3,
				AwayGoals = 1
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var updatedMatch = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(updatedMatch, Is.Not.Null);
		Assert.That(updatedMatch!.State, Is.EqualTo(MatchState.Lost));
	}

	[Test]
	public async Task ClearResult_WhenMatchExists_ShouldClearResult()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var match = CreateMatch("match-1", "season-1", "Test Opponent");
		match.Result = new MatchResult
		{
			HomeGoals = 2,
			AwayGoals = 0
		};
		match.IsCompleted = true;
		match.State = MatchState.Won;

		matchService.Matches.Add(match);

		var result = await controller.ClearResult("match-1", CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var updatedMatch = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(updatedMatch, Is.Not.Null);
		Assert.That(updatedMatch!.Result, Is.Null);
		Assert.That(updatedMatch.IsCompleted, Is.False);
		Assert.That(updatedMatch.State, Is.EqualTo(MatchState.Upcoming));
	}

	[Test]
	public async Task UpdatePlayerStats_WhenMatchExists_ShouldReturnUpdatedStats()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch("match-1", "season-1", "Test Opponent"));

		var playerStats = new List<MatchPlayerStat>
		{
			new()
			{
				PlayerId = "player-1",
				Goals = 2,
				Assists = 1,
				Minutes = 90,
				IsMOTM = true
			}
		};

		var result = await controller.UpdatePlayerStats(
			"match-1",
			playerStats,
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var match = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.PlayerStats, Has.Count.EqualTo(1));
		Assert.That(match.PlayerStats[0].Goals, Is.EqualTo(2));
		Assert.That(match.PlayerStats[0].IsMOTM, Is.True);
	}

	[Test]
	public async Task UpdateNotes_WhenMatchExists_ShouldReturnUpdatedNotes()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch("match-1", "season-1", "Test Opponent"));

		var notes = new MatchNotes
		{
			Availability = "Availability note",
			Tactical = "Tactical note",
			Injuries = "Injury note",
			General = "General note"
		};

		var result = await controller.UpdateNotes(
			"match-1",
			notes,
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var match = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.Notes, Is.Not.Null);
		Assert.That(match.Notes!.Tactical, Is.EqualTo("Tactical note"));
	}

	[Test]
	public async Task Postpone_WhenNewDateIsDefault_ShouldReturnBadRequest()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.Postpone(
			"match-1",
			new PostponeMatchModel
			{
				NewDate = default,
				Reason = "Bad weather"
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Postpone_WhenMatchExists_ShouldPostponeMatch()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var oldDate = new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc);
		var newDate = new DateTime(2026, 8, 8, 14, 0, 0, DateTimeKind.Utc);

		matchService.Matches.Add(CreateMatch("match-1", "season-1", "Test Opponent", oldDate));

		var result = await controller.Postpone(
			"match-1",
			new PostponeMatchModel
			{
				NewDate = newDate,
				Reason = "Bad weather"
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var match = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.Date, Is.EqualTo(newDate));
		Assert.That(match.State, Is.EqualTo(MatchState.Postponed));
		Assert.That(match.Postponements, Has.Count.EqualTo(1));
		Assert.That(match.Postponements[0].OldDate, Is.EqualTo(oldDate));
		Assert.That(match.Postponements[0].NewDate, Is.EqualTo(newDate));
		Assert.That(match.Postponements[0].Reason, Is.EqualTo("Bad weather"));
	}

	[Test]
	public async Task Restore_WhenMatchExists_ShouldRestoreMatchToPreviousDate()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var oldDate = new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc);
		var newDate = new DateTime(2026, 8, 8, 14, 0, 0, DateTimeKind.Utc);

		var match = CreateMatch("match-1", "season-1", "Test Opponent", newDate);
		match.State = MatchState.Postponed;
		match.Postponements.Add(new PostponementAudit
		{
			Id = "postponement-1",
			OldDate = oldDate,
			NewDate = newDate,
			Reason = "Bad weather",
			ChangedAt = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc)
		});

		matchService.Matches.Add(match);

		var result = await controller.Restore("match-1", CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		var updatedMatch = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(updatedMatch, Is.Not.Null);
		Assert.That(updatedMatch!.Date, Is.EqualTo(oldDate));
		Assert.That(updatedMatch.State, Is.EqualTo(MatchState.Upcoming));
		Assert.That(updatedMatch.IsCompleted, Is.False);
	}

	private static Match CreateMatch(
		string id,
		string seasonId,
		string opponent,
		DateTime? date = null
	)
	{
		return new Match
		{
			Id = id,
			SeasonId = seasonId,
			Team = ClubTeam.First,
			Opponent = opponent,
			Date = date ?? new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc),
			Venue = MatchVenue.Home,
			State = MatchState.Upcoming,
			Result = null,
			IsCompleted = false,
			IsLineupLocked = false,
			SelectedFormation = LineupFormation.FourThreeThree,
			Notes = new MatchNotes(),
			Postponements = [],
			SelectedPlayers = [],
			PlayerStats = [],
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
		};
	}

	private class FakeMatchService : IMatchService
	{
		public List<Match> Matches { get; } = [];

		public Task<IReadOnlyList<Match>> GetAllAsync(
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<Match>>(
				Matches
					.OrderBy(match => match.Date)
					.ToList()
			);
		}

		public Task<IReadOnlyList<Match>> GetBySeasonAsync(
			string seasonId,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<Match>>(
				Matches
					.Where(match => match.SeasonId == seasonId)
					.OrderBy(match => match.Date)
					.ToList()
			);
		}

		public Task<Match?> GetByIdAsync(
			string id,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(match => match.Id == id);

			return Task.FromResult(match);
		}

		public Task<Match> CreateAsync(
			Match match,
			CancellationToken cancellationToken = default
		)
		{
			match.Id = string.IsNullOrWhiteSpace(match.Id)
				? Guid.NewGuid().ToString()
				: match.Id;

			match.Opponent = match.Opponent.Trim();
			match.State = MatchState.Upcoming;
			match.IsCompleted = false;
			match.Result = null;
			match.Notes ??= new MatchNotes();
			match.Postponements ??= [];
			match.SelectedPlayers ??= [];
			match.PlayerStats ??= [];
			match.CreatedAt = DateTime.UtcNow;
			match.UpdatedAt = DateTime.UtcNow;

			Matches.Add(match);

			return Task.FromResult(match);
		}

		public Task<Match?> UpdateAsync(
			Match match,
			CancellationToken cancellationToken = default
		)
		{
			var existingMatchIndex = Matches.FindIndex(
				existingMatch => existingMatch.Id == match.Id
			);

			if (existingMatchIndex == -1)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Opponent = match.Opponent.Trim();
			match.UpdatedAt = DateTime.UtcNow;
			Matches[existingMatchIndex] = match;

			return Task.FromResult<Match?>(match);
		}

		public Task<bool> DeleteAsync(
			string id,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(match => match.Id == id);

			if (match is null)
			{
				return Task.FromResult(false);
			}

			Matches.Remove(match);

			return Task.FromResult(true);
		}

		public Task<Match?> SetResultAsync(
			string id,
			MatchResult result,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(match => match.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Result = result;
			match.IsCompleted = true;
			match.State = GetResultState(match.Venue, result);
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> ClearResultAsync(
			string id,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(match => match.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Result = null;
			match.IsCompleted = false;
			match.State = MatchState.Upcoming;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> SetSelectedPlayersAsync(
			string id,
			List<SelectedPlayer> selectedPlayers,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(match => match.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.SelectedPlayers = selectedPlayers;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> SetLineupFormationAsync(
			string id,
			LineupFormation formation,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(match => match.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.SelectedFormation = formation;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> ToggleLineupLockedAsync(
			string id,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(match => match.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.IsLineupLocked = !match.IsLineupLocked;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> UpdateNotesAsync(
			string id,
			MatchNotes notes,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(match => match.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Notes = notes;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> UpdatePlayerStatsAsync(
			string id,
			List<MatchPlayerStat> playerStats,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(match => match.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.PlayerStats = playerStats;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> PostponeAsync(
			string id,
			DateTime newDate,
			string? reason,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(match => match.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Postponements.Add(new PostponementAudit
			{
				Id = Guid.NewGuid().ToString(),
				OldDate = match.Date,
				NewDate = newDate,
				Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
				ChangedAt = DateTime.UtcNow
			});

			match.Date = newDate;
			match.State = MatchState.Postponed;
			match.IsCompleted = false;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> RestoreAsync(
			string id,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(match => match.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			var lastPostponement = match.Postponements.LastOrDefault();

			if (lastPostponement is not null)
			{
				match.Date = lastPostponement.OldDate;
			}

			match.State = MatchState.Upcoming;
			match.IsCompleted = false;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		private static MatchState GetResultState(MatchVenue venue, MatchResult result)
		{
			if (result.HomeGoals == result.AwayGoals)
			{
				return MatchState.Draw;
			}

			var homeWin = result.HomeGoals > result.AwayGoals;

			return venue switch
			{
				MatchVenue.Home => homeWin ? MatchState.Won : MatchState.Lost,
				MatchVenue.Away => homeWin ? MatchState.Lost : MatchState.Won,
				_ => MatchState.Upcoming
			};
		}
	}
}