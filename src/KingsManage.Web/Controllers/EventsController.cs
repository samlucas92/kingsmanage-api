using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/events")]
public class EventsController : ControllerBase
{
	private readonly IClubEventService _eventService;
	private readonly IMatchService _matchService;
	private readonly IClubNotificationService _notificationService;
	private readonly ISeasonService _seasonService;
	private readonly IStatsService _statsService;
	private readonly IUserService _userService;

	public EventsController(
		IClubEventService eventService,
		IMatchService matchService,
		IClubNotificationService notificationService,
		ISeasonService seasonService,
		IStatsService statsService,
		IUserService userService
	)
	{
		_eventService = eventService;
		_matchService = matchService;
		_notificationService = notificationService;
		_seasonService = seasonService;
		_statsService = statsService;
		_userService = userService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<ClubEvent>>> GetAll(
		CancellationToken cancellationToken
	)
	{
		var events = await _eventService.GetAllAsync(cancellationToken);

		return Ok(FilterVisibleEvents(events).ToList());
	}

	[HttpGet("{id}")]
	public async Task<ActionResult<ClubEvent>> GetById(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Event", out var eventId, out var errorResult))
		{
			return errorResult!;
		}

		var clubEvent = await _eventService.GetByIdAsync(eventId, cancellationToken);

		if (clubEvent is null || !CanViewEvent(clubEvent))
		{
			return NotFound();
		}

		return Ok(clubEvent);
	}

	[Authorize(Roles = "Admin,Coach")]
	[HttpPost]
	public async Task<ActionResult<ClubEvent>> Create(
		CreateClubEventModel model,
		CancellationToken cancellationToken
	)
	{
		var validationError = await ValidateCreateEventModelAsync(
			model,
			cancellationToken
		);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var userIdResult = GetCurrentUserId();

		if (!userIdResult.Success)
		{
			return BadRequest(userIdResult.ErrorMessage);
		}

		var clubEvent = model.ToClubEvent();
		clubEvent.Id = clubEvent.Id == Guid.Empty
			? Guid.NewGuid()
			: clubEvent.Id;

		if (model.Type == ClubEventType.Match && model.CreateLinkedMatches)
		{
			var activeSeason = await _seasonService.GetActiveAsync(cancellationToken);

			if (activeSeason is null)
			{
				return BadRequest("An active season must be set before creating linked matches from an event.");
			}

			clubEvent.MatchLinks = [];

			foreach (var matchToCreate in model.CreateMatches)
			{
				var createdMatch = await _matchService.CreateAsync(
					matchToCreate.ToMatch(
						activeSeason.Id,
						model.StartDateTime,
						model.Location,
						clubEvent.Id
					),
					cancellationToken
				);

				clubEvent.MatchLinks.Add(
					new ClubEventMatchLink
					{
						Team = createdMatch.Team,
						MatchId = createdMatch.Id
					}
				);
			}
		}

		var createdEvent = await _eventService.CreateAsync(
			clubEvent,
			cancellationToken
		);

		await SynchroniseMatchEventLinksAsync(
			null,
			createdEvent,
			cancellationToken
		);

		await CreateEventNotificationAsync(
			createdEvent,
			userIdResult.UserId,
			cancellationToken
		);

		return CreatedAtAction(
			nameof(GetById),
			new { id = createdEvent.Id },
			createdEvent
		);
	}

	[Authorize(Roles = "Admin,Coach")]
	[HttpPut("{id}")]
	public async Task<ActionResult<ClubEvent>> Update(
		string id,
		UpdateClubEventModel model,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Event", out var eventId, out var errorResult))
		{
			return errorResult!;
		}

		var existingEvent = await _eventService.GetByIdAsync(eventId, cancellationToken);

		if (existingEvent is null)
		{
			return NotFound();
		}

		var validationError = await ValidateUpdateEventModelAsync(
			model,
			existingEvent.Id,
			cancellationToken
		);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var updatedEvent = await _eventService.UpdateAsync(
			model.ToClubEvent(existingEvent),
			cancellationToken
		);

		if (updatedEvent is null)
		{
			return NotFound();
		}

		await SynchroniseMatchEventLinksAsync(
			existingEvent,
			updatedEvent,
			cancellationToken
		);

		var userIdResult = GetCurrentUserId();

		if (!userIdResult.Success)
		{
			return BadRequest(userIdResult.ErrorMessage);
		}

		await CreateEventUpdatedNotificationAsync(
			updatedEvent,
			userIdResult.UserId,
			cancellationToken
		);

		return Ok(updatedEvent);
	}

	[Authorize(Roles = "Admin,Coach")]
	[HttpDelete("{id}")]
	public async Task<IActionResult> Delete(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Event", out var eventId, out var errorResult))
		{
			return errorResult!;
		}

		var existingEvent = await _eventService.GetByIdAsync(eventId, cancellationToken);

		if (existingEvent is null)
		{
			return NotFound();
		}

		var linkedMatchIds = existingEvent.MatchLinks
			.Where(matchLink => matchLink.MatchId.HasValue)
			.Select(matchLink => matchLink.MatchId!.Value)
			.Distinct()
			.ToList();

		var linkedMatches = new List<Match>();

		foreach (var matchId in linkedMatchIds)
		{
			var linkedMatch = await _matchService.GetByIdAsync(matchId, cancellationToken);

			if (linkedMatch is not null)
			{
				linkedMatches.Add(linkedMatch);
			}
		}

		var deleted = await _eventService.DeleteAsync(eventId, cancellationToken);

		if (!deleted)
		{
			return NotFound();
		}

		foreach (var match in linkedMatches)
		{
			await _matchService.DeleteAsync(match.Id, cancellationToken);
		}

		await RecalculateDeletedLinkedMatchesAsync(linkedMatches, cancellationToken);

		return NoContent();
	}

	[HttpPut("{id}/seen")]
	public async Task<ActionResult<ClubEvent>> MarkSeen(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Event", out var eventId, out var errorResult))
		{
			return errorResult!;
		}

		var playerIdResult = await GetCurrentUserPlayerIdAsync(cancellationToken);

		if (!playerIdResult.Success)
		{
			return BadRequest(playerIdResult.ErrorMessage);
		}

		var clubEvent = await _eventService.GetByIdAsync(eventId, cancellationToken);

		if (clubEvent is null || !CanViewEvent(clubEvent))
		{
			return NotFound();
		}

		var updatedEvent = await _eventService.MarkSeenAsync(
			eventId,
			playerIdResult.PlayerId,
			cancellationToken
		);

		if (updatedEvent is null)
		{
			return NotFound();
		}

		return Ok(updatedEvent);
	}

	[HttpPut("{id}/availability")]
	public async Task<ActionResult<ClubEvent>> SetOwnAvailability(
		string id,
		UpdateClubEventAvailabilityModel model,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Event", out var eventId, out var errorResult))
		{
			return errorResult!;
		}

		var playerIdResult = await GetCurrentUserPlayerIdAsync(cancellationToken);

		if (!playerIdResult.Success)
		{
			return BadRequest(playerIdResult.ErrorMessage);
		}

		var clubEvent = await _eventService.GetByIdAsync(eventId, cancellationToken);

		if (clubEvent is null || !CanViewEvent(clubEvent))
		{
			return NotFound();
		}

		var updatedEvent = await _eventService.SetAvailabilityAsync(
			eventId,
			playerIdResult.PlayerId,
			model.Status,
			cancellationToken
		);

		if (updatedEvent is null)
		{
			return NotFound();
		}

		return Ok(updatedEvent);
	}

	[Authorize(Roles = "Admin,Coach")]
	[HttpPut("{id}/availability/{playerId}")]
	public async Task<ActionResult<ClubEvent>> SetPlayerAvailability(
		string id,
		string playerId,
		UpdateClubEventAvailabilityModel model,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Event", out var eventId, out var eventErrorResult))
		{
			return eventErrorResult!;
		}

		if (!TryParseGuid(playerId, "Player", out var parsedPlayerId, out var playerErrorResult))
		{
			return playerErrorResult!;
		}

		var clubEvent = await _eventService.GetByIdAsync(eventId, cancellationToken);

		if (clubEvent is null)
		{
			return NotFound();
		}

		var updatedEvent = await _eventService.SetAvailabilityAsync(
			eventId,
			parsedPlayerId,
			model.Status,
			cancellationToken
		);

		if (updatedEvent is null)
		{
			return NotFound();
		}

		return Ok(updatedEvent);
	}

	private async Task CreateEventNotificationAsync(
		ClubEvent clubEvent,
		Guid createdByUserId,
		CancellationToken cancellationToken
	)
	{
		await CreateEventNotificationAsync(
			clubEvent,
			createdByUserId,
			NotificationType.NewEvent,
			$"New event: {clubEvent.Title}",
			$"A new {FormatEventType(clubEvent.Type)} event has been created.",
			cancellationToken
		);
	}

	private async Task CreateEventUpdatedNotificationAsync(
		ClubEvent clubEvent,
		Guid createdByUserId,
		CancellationToken cancellationToken
	)
	{
		await CreateEventNotificationAsync(
			clubEvent,
			createdByUserId,
			NotificationType.EventUpdated,
			$"Event updated: {clubEvent.Title}",
			$"A {FormatEventType(clubEvent.Type)} event has been updated.",
			cancellationToken
		);
	}

	private async Task CreateEventNotificationAsync(
		ClubEvent clubEvent,
		Guid createdByUserId,
		NotificationType notificationType,
		string title,
		string message,
		CancellationToken cancellationToken
	)
	{
		var recipients = await GetVisibleActiveUsersExceptAsync(
			clubEvent,
			createdByUserId,
			cancellationToken
		);

		if (recipients.Count == 0)
		{
			return;
		}

		await _notificationService.CreateAsync(
			new ClubNotification
			{
				Type = notificationType,
				SourceType = NotificationSourceType.Event,
				SourceId = clubEvent.Id,
				Title = title,
				Message = message,
				ActionPath = "/dashboard?tab=events",
				CreatedByUserId = createdByUserId,
				CreatedByUserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
				Recipients = recipients
					.Select(user => new ClubNotificationRecipient { UserId = user.Id })
					.ToList()
			},
			cancellationToken
		);
	}

	private async Task<List<AppUser>> GetVisibleActiveUsersExceptAsync(
		ClubEvent clubEvent,
		Guid excludedUserId,
		CancellationToken cancellationToken
	)
	{
		var users = await _userService.GetAllAsync(cancellationToken);

		return users
			.Where(user => user.IsActive)
			.Where(user => user.Id != excludedUserId)
			.Where(user => CanUserRoleViewEvent(user.Role, clubEvent))
			.ToList();
	}

	private static bool CanUserRoleViewEvent(UserRole role, ClubEvent clubEvent)
	{
		if (role == UserRole.Player)
		{
			return clubEvent.Type is
				ClubEventType.Match or
				ClubEventType.Training or
				ClubEventType.Social;
		}

		return true;
	}

	private static string FormatEventType(ClubEventType type)
	{
		return type.ToString().ToLowerInvariant();
	}

	private IEnumerable<ClubEvent> FilterVisibleEvents(IEnumerable<ClubEvent> events)
	{
		if (User.IsInRole(nameof(UserRole.Player)))
		{
			return events.Where(CanViewEvent);
		}

		return events;
	}

	private async Task RecalculateDeletedLinkedMatchesAsync(
		IEnumerable<Match> deletedMatches,
		CancellationToken cancellationToken
	)
	{
		var affectedSeasonIds = deletedMatches
			.Where(match => match.SeasonId.HasValue)
			.Select(match => match.SeasonId!.Value)
			.Distinct()
			.ToList();

		foreach (var seasonId in affectedSeasonIds)
		{
			await _statsService.RecalculateSeasonStatsAsync(
				seasonId,
				cancellationToken
			);
		}
	}

	private bool CanViewEvent(ClubEvent clubEvent)
	{
		if (User.IsInRole(nameof(UserRole.Player)))
		{
			return clubEvent.Type is
				ClubEventType.Match or
				ClubEventType.Training or
				ClubEventType.Social;
		}

		return true;
	}

	private async Task<string?> ValidateCreateEventModelAsync(
		CreateClubEventModel model,
		CancellationToken cancellationToken
	)
	{
		var matchLinks = model.MatchLinks.Select(matchLink => matchLink.ToMatchLink()).ToList();

		var sharedValidationError = ValidateSharedEventFields(
			model.Type,
			model.TeamScope,
			model.Title,
			model.StartDateTime,
			model.EndDateTime,
			matchLinks
		);

		if (sharedValidationError is not null)
		{
			return sharedValidationError;
		}

		if (model.Type != ClubEventType.Match)
		{
			if (model.MatchLinks.Count > 0)
			{
				return "Only match events can have match links.";
			}

			if (model.CreateLinkedMatches || model.CreateMatches.Count > 0)
			{
				return "Only match events can create linked matches.";
			}

			return null;
		}

		if (model.CreateLinkedMatches && model.MatchLinks.Count > 0)
		{
			return "A match event cannot link existing matches and create new matches at the same time.";
		}

		if (!model.CreateLinkedMatches && model.CreateMatches.Count > 0)
		{
			return "Set createLinkedMatches to true to create and link new matches.";
		}

		if (model.CreateLinkedMatches)
		{
			if (model.CreateMatches.Count == 0)
			{
				return "Match details are required when creating linked matches.";
			}

			var duplicateCreateTeam = model.CreateMatches
				.GroupBy(match => match.Team)
				.FirstOrDefault(group => group.Count() > 1);

			if (duplicateCreateTeam is not null)
			{
				return "A match event cannot create duplicate matches for the same team.";
			}

			var teamCoverageError = ValidateTeamCoverage(
				model.CreateMatches.Select(match => match.Team),
				model.TeamScope,
				"Created matches"
			);

			if (teamCoverageError is not null)
			{
				return teamCoverageError;
			}

			foreach (var createMatchModel in model.CreateMatches)
			{
				if (string.IsNullOrWhiteSpace(createMatchModel.Opponent))
				{
					return "Match opponent is required.";
				}

				if (!IsTeamInScope(createMatchModel.Team, model.TeamScope))
				{
					return "Created match team must be inside the event team scope.";
				}
			}

			return null;
		}

		return await ValidateMatchLinksAsync(
			matchLinks,
			model.TeamScope,
			null,
			cancellationToken
		);
	}

	private async Task<string?> ValidateUpdateEventModelAsync(
		UpdateClubEventModel model,
		Guid eventId,
		CancellationToken cancellationToken
	)
	{
		var matchLinks = model.MatchLinks.Select(matchLink => matchLink.ToMatchLink()).ToList();

		var sharedValidationError = ValidateSharedEventFields(
			model.Type,
			model.TeamScope,
			model.Title,
			model.StartDateTime,
			model.EndDateTime,
			matchLinks
		);

		if (sharedValidationError is not null)
		{
			return sharedValidationError;
		}

		if (model.Type != ClubEventType.Match && model.MatchLinks.Count > 0)
		{
			return "Only match events can have match links.";
		}

		return await ValidateMatchLinksAsync(
			matchLinks,
			model.TeamScope,
			eventId,
			cancellationToken
		);
	}

	private static string? ValidateSharedEventFields(
		ClubEventType type,
		ClubEventTeamScope teamScope,
		string title,
		DateTime startDateTime,
		DateTime? endDateTime,
		List<ClubEventMatchLink> matchLinks
	)
	{
		if (string.IsNullOrWhiteSpace(title))
		{
			return "Event title is required.";
		}

		if (startDateTime == default)
		{
			return "Event start date is required.";
		}

		if (endDateTime.HasValue && endDateTime.Value < startDateTime)
		{
			return "Event end date cannot be before the start date.";
		}

		if (type == ClubEventType.Match)
		{
			var duplicateTeam = matchLinks
				.GroupBy(matchLink => matchLink.Team)
				.FirstOrDefault(group => group.Count() > 1);

			if (duplicateTeam is not null)
			{
				return "A match event cannot have duplicate match links for the same team.";
			}

			var duplicateMatchId = matchLinks
				.Where(matchLink => matchLink.MatchId.HasValue)
				.GroupBy(matchLink => matchLink.MatchId!.Value)
				.FirstOrDefault(group => group.Count() > 1);

			if (duplicateMatchId is not null)
			{
				return "A match event cannot link the same match more than once.";
			}
		}

		return null;
	}

	private async Task<string?> ValidateMatchLinksAsync(
		List<ClubEventMatchLink> matchLinks,
		ClubEventTeamScope teamScope,
		Guid? currentEventId,
		CancellationToken cancellationToken
	)
	{
		if (matchLinks.Count > 0)
		{
			var teamCoverageError = ValidateTeamCoverage(
				matchLinks.Select(matchLink => matchLink.Team),
				teamScope,
				"Linked matches"
			);

			if (teamCoverageError is not null)
			{
				return teamCoverageError;
			}
		}

		foreach (var matchLink in matchLinks)
		{
			if (!IsTeamInScope(matchLink.Team, teamScope))
			{
				return "Linked match team must be inside the event team scope.";
			}

			if (!matchLink.MatchId.HasValue || matchLink.MatchId.Value == Guid.Empty)
			{
				return "Linked match id is required.";
			}

			var linkedMatch = await _matchService.GetByIdAsync(
				matchLink.MatchId.Value,
				cancellationToken
			);

			if (linkedMatch is null)
			{
				return "Linked match was not found.";
			}

			if (linkedMatch.Team != matchLink.Team)
			{
				return "Linked match team must match the selected event team.";
			}

			if (
				linkedMatch.ClubEventId.HasValue &&
				(!currentEventId.HasValue || linkedMatch.ClubEventId.Value != currentEventId.Value)
			)
			{
				return "Linked match is already linked to another event.";
			}
		}

		return null;
	}

	private static string? ValidateTeamCoverage(
		IEnumerable<ClubTeam> teams,
		ClubEventTeamScope teamScope,
		string label
	)
	{
		var selectedTeams = teams.Distinct().ToList();
		var expectedTeams = GetTeamsForScope(teamScope);

		if (selectedTeams.Any(team => !expectedTeams.Contains(team)))
		{
			return $"{label} must be inside the event team scope.";
		}

		if (selectedTeams.Count == 0)
		{
			return null;
		}

		var missingTeams = expectedTeams
			.Where(expectedTeam => !selectedTeams.Contains(expectedTeam))
			.ToList();

		if (missingTeams.Count > 0)
		{
			return $"{label} must include separate details for every team in the event scope.";
		}

		return null;
	}

	private static List<ClubTeam> GetTeamsForScope(ClubEventTeamScope teamScope)
	{
		return teamScope switch
		{
			ClubEventTeamScope.First => [ClubTeam.First],
			ClubEventTeamScope.Second => [ClubTeam.Second],
			ClubEventTeamScope.Both => [ClubTeam.First, ClubTeam.Second],
			_ => []
		};
	}

	private static bool IsTeamInScope(ClubTeam team, ClubEventTeamScope teamScope)
	{
		return teamScope switch
		{
			ClubEventTeamScope.First => team == ClubTeam.First,
			ClubEventTeamScope.Second => team == ClubTeam.Second,
			ClubEventTeamScope.Both => true,
			_ => false
		};
	}

	private async Task SynchroniseMatchEventLinksAsync(
		ClubEvent? previousEvent,
		ClubEvent? updatedEvent,
		CancellationToken cancellationToken
	)
	{
		var eventId = updatedEvent?.Id ?? previousEvent?.Id;

		if (!eventId.HasValue)
		{
			return;
		}

		var previousMatchIds = GetLinkedMatchIds(previousEvent).ToList();
		var updatedMatchIds = GetLinkedMatchIds(updatedEvent).ToList();

		foreach (var removedMatchId in previousMatchIds.Except(updatedMatchIds))
		{
			var match = await _matchService.GetByIdAsync(
				removedMatchId,
				cancellationToken
			);

			if (match is null || match.ClubEventId != eventId.Value)
			{
				continue;
			}

			match.ClubEventId = null;

			await _matchService.UpdateAsync(match, cancellationToken);
		}

		foreach (var updatedMatchId in updatedMatchIds)
		{
			var match = await _matchService.GetByIdAsync(
				updatedMatchId,
				cancellationToken
			);

			if (match is null || match.ClubEventId == eventId.Value)
			{
				continue;
			}

			match.ClubEventId = eventId.Value;

			await _matchService.UpdateAsync(match, cancellationToken);
		}
	}

	private static IEnumerable<Guid> GetLinkedMatchIds(ClubEvent? clubEvent)
	{
		if (clubEvent?.MatchLinks is null)
		{
			return [];
		}

		return clubEvent.MatchLinks
			.Where(matchLink => matchLink.MatchId.HasValue)
			.Select(matchLink => matchLink.MatchId!.Value);
	}

	private CurrentUserIdResult GetCurrentUserId()
	{
		var userIdClaim =
			User.FindFirstValue(ClaimTypes.NameIdentifier) ??
			User.FindFirstValue("sub") ??
			User.FindFirstValue("id");

		if (string.IsNullOrWhiteSpace(userIdClaim))
		{
			return CurrentUserIdResult.Failed("Current user id was not found in the auth token.");
		}

		if (!Guid.TryParse(userIdClaim, out var userId))
		{
			return CurrentUserIdResult.Failed("Current user id in the auth token is invalid.");
		}

		return CurrentUserIdResult.SuccessResult(userId);
	}

	private async Task<PlayerIdResult> GetCurrentUserPlayerIdAsync(
		CancellationToken cancellationToken
	)
	{
		var userIdClaim =
			User.FindFirstValue(ClaimTypes.NameIdentifier) ??
			User.FindFirstValue("sub") ??
			User.FindFirstValue("id");

		if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
		{
			return PlayerIdResult.Failure("Could not identify the signed-in user.");
		}

		var user = await _userService.GetByIdAsync(userId, cancellationToken);

		if (user?.PlayerId is null || user.PlayerId == Guid.Empty)
		{
			return PlayerIdResult.Failure("The signed-in user is not linked to a player.");
		}

		return PlayerIdResult.SuccessResult(user.PlayerId.Value);
	}

	private bool TryParseGuid(
		string id,
		string entityName,
		out Guid parsedId,
		out BadRequestObjectResult? errorResult
	)
	{
		parsedId = Guid.Empty;
		errorResult = null;

		if (string.IsNullOrWhiteSpace(id))
		{
			errorResult = BadRequest($"{entityName} id is required.");
			return false;
		}

		if (!Guid.TryParse(id, out parsedId))
		{
			errorResult = BadRequest($"{entityName} id must be a valid GUID.");
			return false;
		}

		return true;
	}

	private sealed record CurrentUserIdResult(
		bool Success,
		Guid UserId,
		string ErrorMessage
	)
	{
		public static CurrentUserIdResult SuccessResult(Guid userId) =>
			new(true, userId, string.Empty);

		public static CurrentUserIdResult Failed(string errorMessage) =>
			new(false, Guid.Empty, errorMessage);
	}

	private sealed record PlayerIdResult(
		bool Success,
		Guid PlayerId,
		string ErrorMessage
	)
	{
		public static PlayerIdResult SuccessResult(Guid playerId)
		{
			return new PlayerIdResult(true, playerId, string.Empty);
		}

		public static PlayerIdResult Failure(string errorMessage)
		{
			return new PlayerIdResult(false, Guid.Empty, errorMessage);
		}
	}
}
