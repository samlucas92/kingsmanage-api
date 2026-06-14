using System.Collections;
using KingsManage;
using KingsManage.Web;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public class MatchesControllerTests
{
	private static readonly Guid MatchOneId = Guid.Parse("33333333-3333-3333-3333-333333333333");
	private static readonly Guid MatchTwoId = Guid.Parse("44444444-4444-4444-4444-444444444444");
	private static readonly Guid MissingMatchId = Guid.Parse("99999999-9999-9999-9999-999999999999");
	private static readonly Guid SeasonOneId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
	private static readonly Guid SeasonTwoId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
	private static readonly Guid PlayerOneId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	[Test]
	public async Task GetAll_WhenNoSeasonIdProvided_ShouldReturnAllMatches()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch(MatchOneId, SeasonOneId, "Team One"));
		matchService.Matches.Add(CreateMatch(MatchTwoId, SeasonTwoId, "Team Two"));

		var result = await controller.GetAll(null, CancellationToken.None);
		var okResult = result.Result as OkObjectResult;
		var matches = GetResultItems(okResult);

		Assert.That(okResult, Is.Not.Null);
		Assert.That(matches, Is.Not.Null);
		Assert.That(matches, Has.Count.EqualTo(2));
	}

	[Test]
	public async Task GetAll_WhenSeasonIdProvided_ShouldReturnSeasonMatches()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch(MatchOneId, SeasonOneId, "Team One"));
		matchService.Matches.Add(CreateMatch(MatchTwoId, SeasonTwoId, "Team Two"));

		var result = await controller.GetAll(SeasonOneId.ToString(), CancellationToken.None);
		var okResult = result.Result as OkObjectResult;
		var matches = GetResultItems(okResult);

		Assert.That(okResult, Is.Not.Null);
		Assert.That(matches, Is.Not.Null);
		Assert.That(matches, Has.Count.EqualTo(1));
		Assert.That(GetGuidProperty(matches![0], nameof(Match.SeasonId)), Is.EqualTo(SeasonOneId));
	}

	[Test]
	public async Task GetAll_WhenSeasonIdIsNotGuid_ShouldReturnBadRequest()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.GetAll("not-a-guid", CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
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
	public async Task GetById_WhenIdIsNotGuid_ShouldReturnBadRequest()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.GetById("not-a-guid", CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task GetById_WhenMatchDoesNotExist_ShouldReturnNotFound()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.GetById(MissingMatchId.ToString(), CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task GetById_WhenMatchExists_ShouldReturnMatch()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var result = await controller.GetById(MatchOneId.ToString(), CancellationToken.None);
		var okResult = result.Result as OkObjectResult;
		var match = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.Id, Is.EqualTo(MatchOneId));
		Assert.That(match.Opponent, Is.EqualTo("Test Opponent"));
	}

	[Test]
	public async Task Create_WhenOpponentIsEmpty_ShouldReturnBadRequest()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.Create(
			new Match
			{
				SeasonId = SeasonOneId,
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
	public async Task Create_WhenMatchIsValid_ShouldReturnCreatedMatch()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.Create(
			new Match
			{
				SeasonId = SeasonOneId,
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
		Assert.That(match!.Id, Is.Not.EqualTo(Guid.Empty));
		Assert.That(match.Opponent, Is.EqualTo("Test Opponent"));
		Assert.That(match.State, Is.EqualTo(MatchState.Upcoming));
		Assert.That(matchService.Matches, Has.Count.EqualTo(1));
	}

	[Test]
	public async Task Update_WhenIdIsNotGuid_ShouldReturnBadRequest()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.Update(
			"not-a-guid",
			CreateMatch(MatchOneId, SeasonOneId, "Updated Opponent"),
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Update_WhenMatchDoesNotExist_ShouldReturnNotFound()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		var result = await controller.Update(
			MissingMatchId.ToString(),
			CreateMatch(Guid.Empty, SeasonOneId, "Updated Opponent"),
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
			Id = MatchOneId,
			SeasonId = SeasonOneId,
			Team = ClubTeam.First,
			Opponent = "Old Opponent",
			Date = new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc),
			Venue = MatchVenue.Home,
			State = MatchState.Upcoming,
			SelectedFormation = LineupFormation.FourThreeThree,
			CreatedAt = createdAt,
			UpdatedAt = createdAt
		});

		var result = await controller.Update(
			MatchOneId.ToString(),
			new Match
			{
				Id = Guid.NewGuid(),
				SeasonId = SeasonOneId,
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
		Assert.That(match!.Id, Is.EqualTo(MatchOneId));
		Assert.That(match.Opponent, Is.EqualTo("Updated Opponent"));
		Assert.That(match.Team, Is.EqualTo(ClubTeam.Second));
		Assert.That(match.CreatedAt, Is.EqualTo(createdAt));
	}

	[Test]
	public async Task Delete_WhenMatchExists_ShouldReturnNoContent()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var result = await controller.Delete(MatchOneId.ToString(), CancellationToken.None);

		Assert.That(result, Is.TypeOf<NoContentResult>());
		Assert.That(matchService.Matches, Is.Empty);
	}

	[Test]
	public async Task SetLineup_WhenMatchExists_ShouldReturnUpdatedMatch()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var selectedPlayers = new List<SelectedPlayer>
		{
			new() { PlayerId = PlayerOneId, X = 50, Y = 60, Area = "pitch", PositionIndex = 1 }
		};

		var result = await controller.SetLineup(MatchOneId.ToString(), selectedPlayers, CancellationToken.None);
		var okResult = result.Result as OkObjectResult;
		var match = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.SelectedPlayers, Has.Count.EqualTo(1));
		Assert.That(match.SelectedPlayers[0].PlayerId, Is.EqualTo(PlayerOneId));
	}

	[Test]
	public async Task SetFormation_WhenMatchExists_ShouldReturnUpdatedFormation()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var result = await controller.SetFormation(
			MatchOneId.ToString(),
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

		matchService.Matches.Add(CreateMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var result = await controller.ToggleLineupLocked(MatchOneId.ToString(), CancellationToken.None);
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
			MatchOneId.ToString(),
			new MatchResult { HomeGoals = -1, AwayGoals = 0 },
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task SetResult_WhenHomeMatchWon_ShouldReturnCompletedWonMatch()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var result = await controller.SetResult(
			MatchOneId.ToString(),
			new MatchResult { HomeGoals = 3, AwayGoals = 1 },
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
		var match = CreateMatch(MatchOneId, SeasonOneId, "Test Opponent");
		match.Venue = MatchVenue.Away;
		matchService.Matches.Add(match);

		var result = await controller.SetResult(
			MatchOneId.ToString(),
			new MatchResult { HomeGoals = 3, AwayGoals = 1 },
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
		var match = CreateMatch(MatchOneId, SeasonOneId, "Test Opponent");
		match.Result = new MatchResult { HomeGoals = 2, AwayGoals = 0 };
		match.IsCompleted = true;
		match.State = MatchState.Won;
		matchService.Matches.Add(match);

		var result = await controller.ClearResult(MatchOneId.ToString(), CancellationToken.None);
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

		matchService.Matches.Add(CreateMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var playerStats = new List<MatchPlayerStats>
		{
			new() { PlayerId = PlayerOneId, Goals = 2, Assists = 1, Minutes = 90, IsMOTM = true }
		};

		var result = await controller.UpdatePlayerStats(
			MatchOneId.ToString(),
			playerStats,
			CancellationToken.None
		);
		var okResult = result.Result as OkObjectResult;
		var match = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(match, Is.Not.Null);
		Assert.That(match!.PlayerStats, Has.Count.EqualTo(1));
		Assert.That(match.PlayerStats[0].Goals, Is.EqualTo(2));
	}

	[Test]
	public async Task UpdateNotes_WhenMatchExists_ShouldReturnUpdatedNotes()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);

		matchService.Matches.Add(CreateMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var notes = new MatchNotes
		{
			Availability = "Availability note",
			Tactical = "Tactical note",
			Injuries = "Injury note",
			General = "General note"
		};

		var result = await controller.UpdateNotes(
			MatchOneId.ToString(),
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
			MatchOneId.ToString(),
			new PostponeMatchModel { NewDate = default, Reason = "Bad weather" },
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

		matchService.Matches.Add(CreateMatch(MatchOneId, SeasonOneId, "Test Opponent", oldDate));

		var result = await controller.Postpone(
			MatchOneId.ToString(),
			new PostponeMatchModel { NewDate = newDate, Reason = "Bad weather" },
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
	}

	[Test]
	public async Task Restore_WhenMatchExists_ShouldRestoreMatchToPreviousDate()
	{
		var matchService = new FakeMatchService();
		var controller = new MatchesController(matchService);
		var oldDate = new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc);
		var newDate = new DateTime(2026, 8, 8, 14, 0, 0, DateTimeKind.Utc);
		var match = CreateMatch(MatchOneId, SeasonOneId, "Test Opponent", newDate);
		match.State = MatchState.Postponed;
		match.Postponements.Add(new PostponementAudit
		{
			Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
			OldDate = oldDate,
			NewDate = newDate,
			Reason = "Bad weather",
			ChangedAt = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc)
		});
		matchService.Matches.Add(match);

		var result = await controller.Restore(MatchOneId.ToString(), CancellationToken.None);
		var okResult = result.Result as OkObjectResult;
		var updatedMatch = okResult?.Value as Match;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(updatedMatch, Is.Not.Null);
		Assert.That(updatedMatch!.Date, Is.EqualTo(oldDate));
		Assert.That(updatedMatch.State, Is.EqualTo(MatchState.Upcoming));
		Assert.That(updatedMatch.IsCompleted, Is.False);
	}

	private static List<object>? GetResultItems(OkObjectResult? okResult)
	{
		return okResult?.Value is IEnumerable items
			? items.Cast<object>().ToList()
			: null;
	}

	private static Guid? GetGuidProperty(object item, string propertyName)
	{
		var property = item.GetType().GetProperty(propertyName);
		var value = property?.GetValue(item);

		if (value is Guid guid)
		{
			return guid;
		}

		return null;
	}

	private static Match CreateMatch(
		Guid id,
		Guid seasonId,
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
				Matches.OrderBy(match => match.Date).ToList()
			);
		}

		public Task<IReadOnlyList<Match>> GetBySeasonAsync(
			Guid seasonId,
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
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(Matches.FirstOrDefault(match => match.Id == id));
		}

		public Task<Match> CreateAsync(
			Match match,
			CancellationToken cancellationToken = default
		)
		{
			match.Id = match.Id == Guid.Empty ? Guid.NewGuid() : match.Id;
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
			var index = Matches.FindIndex(existingMatch => existingMatch.Id == match.Id);
			if (index == -1)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Opponent = match.Opponent.Trim();
			match.UpdatedAt = DateTime.UtcNow;
			Matches[index] = match;

			return Task.FromResult<Match?>(match);
		}

		public Task<bool> DeleteAsync(
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);
			if (match is null)
			{
				return Task.FromResult(false);
			}

			Matches.Remove(match);
			return Task.FromResult(true);
		}

		public Task<Match?> SetResultAsync(
			Guid id,
			MatchResult result,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);
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
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);
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
			Guid id,
			List<SelectedPlayer> selectedPlayers,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);
			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.SelectedPlayers = selectedPlayers;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> SetLineupFormationAsync(
			Guid id,
			LineupFormation formation,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);
			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.SelectedFormation = formation;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> ToggleLineupLockedAsync(
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);
			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.IsLineupLocked = !match.IsLineupLocked;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> UpdateNotesAsync(
			Guid id,
			MatchNotes notes,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);
			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Notes = notes;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> UpdatePlayerStatsAsync(
			Guid id,
			List<MatchPlayerStats> playerStats,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);
			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.PlayerStats = playerStats;
			match.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> PostponeAsync(
			Guid id,
			DateTime newDate,
			string? reason,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);
			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Postponements.Add(new PostponementAudit
			{
				Id = Guid.NewGuid(),
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
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);
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
