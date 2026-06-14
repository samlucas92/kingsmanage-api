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
	private readonly IUserService _userService;

	public EventsController(
		IClubEventService eventService,
		IMatchService matchService,
		IUserService userService
	)
	{
		_eventService = eventService;
		_matchService = matchService;
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

		var clubEvent = model.ToClubEvent();

		if (model.Type == ClubEventType.Match && model.CreateLinkedMatches)
		{
			clubEvent.MatchLinks = [];

			foreach (var matchToCreate in model.CreateMatches)
			{
				var createdMatch = await _matchService.CreateAsync(
					matchToCreate.ToMatch(model.StartDateTime),
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

		var deleted = await _eventService.DeleteAsync(eventId, cancellationToken);

		if (!deleted)
		{
			return NotFound();
		}

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

	private IEnumerable<ClubEvent> FilterVisibleEvents(IEnumerable<ClubEvent> events)
	{
		if (User.IsInRole(nameof(UserRole.Player)))
		{
			return events.Where(CanViewEvent);
		}

		return events;
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
			cancellationToken
		);
	}

	private async Task<string?> ValidateUpdateEventModelAsync(
		UpdateClubEventModel model,
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
		}

		return null;
	}

	private async Task<string?> ValidateMatchLinksAsync(
		List<ClubEventMatchLink> matchLinks,
		ClubEventTeamScope teamScope,
		CancellationToken cancellationToken
	)
	{
		foreach (var matchLink in matchLinks)
		{
			if (!IsTeamInScope(matchLink.Team, teamScope))
			{
				return "Linked match team must be inside the event team scope.";
			}

			if (matchLink.MatchId.HasValue)
			{
				var linkedMatch = await _matchService.GetByIdAsync(
					matchLink.MatchId.Value,
					cancellationToken
				);

				if (linkedMatch is null)
				{
					return "Linked match was not found.";
				}
			}
		}

		return null;
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
