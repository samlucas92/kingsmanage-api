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
		var validationError = await ValidateEventModelAsync(
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

		var createdEvent = await _eventService.CreateAsync(
			model.ToClubEvent(),
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

		var validationError = await ValidateEventModelAsync(
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

	private async Task<string?> ValidateEventModelAsync(
		ClubEventType type,
		string title,
		DateTime startDateTime,
		DateTime? endDateTime,
		Guid? matchId,
		CancellationToken cancellationToken
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
