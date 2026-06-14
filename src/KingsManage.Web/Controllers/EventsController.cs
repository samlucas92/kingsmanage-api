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

	public EventsController(
		IClubEventService eventService,
		IMatchService matchService
	)
	{
		_eventService = eventService;
		_matchService = matchService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<ClubEvent>>> GetAll(
		[FromQuery] string? seasonId,
		CancellationToken cancellationToken
	)
	{
		IReadOnlyList<ClubEvent> events;

		if (!string.IsNullOrWhiteSpace(seasonId))
		{
			if (!Guid.TryParse(seasonId, out var parsedSeasonId))
			{
				return BadRequest("Season id must be a valid GUID.");
			}

			events = await _eventService.GetBySeasonAsync(
				parsedSeasonId,
				cancellationToken
			);
		}
		else
		{
			events = await _eventService.GetAllAsync(cancellationToken);
		}

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

		if (
			model.Type == ClubEventType.Match &&
			model.CreateLinkedMatch &&
			model.CreateMatch is not null
		)
		{
			var createdMatch = await _matchService.CreateAsync(
				model.CreateMatch.ToMatch(model.StartDateTime),
				cancellationToken
			);

			clubEvent.MatchId = createdMatch.Id;
			clubEvent.SeasonId ??= createdMatch.SeasonId;
			clubEvent.Team ??= createdMatch.Team;
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
			model.Type,
			model.Title,
			model.StartDateTime,
			model.EndDateTime,
			model.MatchId,
			cancellationToken
		);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var updatedEvent = await _eventService.UpdateAsync(
			model.ToClubEvent(eventId, existingEvent.CreatedAt),
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
		var sharedValidationError = ValidateSharedEventFields(
			model.Title,
			model.StartDateTime,
			model.EndDateTime
		);

		if (sharedValidationError is not null)
		{
			return sharedValidationError;
		}

		if (model.Type != ClubEventType.Match)
		{
			if (model.MatchId.HasValue)
			{
				return "Only match events can be linked to a match.";
			}

			if (model.CreateLinkedMatch || model.CreateMatch is not null)
			{
				return "Only match events can create a linked match.";
			}

			return null;
		}

		if (model.MatchId.HasValue && model.CreateLinkedMatch)
		{
			return "A match event cannot link to an existing match and create a new match at the same time.";
		}

		if (model.MatchId.HasValue)
		{
			var linkedMatch = await _matchService.GetByIdAsync(
				model.MatchId.Value,
				cancellationToken
			);

			if (linkedMatch is null)
			{
				return "Linked match was not found.";
			}

			return null;
		}

		if (model.CreateMatch is not null && !model.CreateLinkedMatch)
		{
			return "Set createLinkedMatch to true to create and link a new match.";
		}

		if (model.CreateLinkedMatch)
		{
			if (model.CreateMatch is null)
			{
				return "Match details are required when creating a linked match.";
			}

			return ValidateCreateMatchModel(model.CreateMatch);
		}

		return null;
	}

	private async Task<string?> ValidateUpdateEventModelAsync(
		ClubEventType type,
		string title,
		DateTime startDateTime,
		DateTime? endDateTime,
		Guid? matchId,
		CancellationToken cancellationToken
	)
	{
		var sharedValidationError = ValidateSharedEventFields(
			title,
			startDateTime,
			endDateTime
		);

		if (sharedValidationError is not null)
		{
			return sharedValidationError;
		}

		if (type != ClubEventType.Match && matchId.HasValue)
		{
			return "Only match events can be linked to a match.";
		}

		if (type == ClubEventType.Match && matchId.HasValue)
		{
			var linkedMatch = await _matchService.GetByIdAsync(
				matchId.Value,
				cancellationToken
			);

			if (linkedMatch is null)
			{
				return "Linked match was not found.";
			}
		}

		return null;
	}

	private static string? ValidateSharedEventFields(
		string title,
		DateTime startDateTime,
		DateTime? endDateTime
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

		return null;
	}

	private static string? ValidateCreateMatchModel(CreateMatchForEventModel matchModel)
	{
		if (string.IsNullOrWhiteSpace(matchModel.Opponent))
		{
			return "Match opponent is required.";
		}

		return null;
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
}
