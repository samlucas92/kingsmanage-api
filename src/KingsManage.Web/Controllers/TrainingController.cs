using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/training")]
public class TrainingController : ControllerBase
{
	private readonly IClubEventService eventService;
	private readonly IPlayerService playerService;
	private readonly ITrainingDevelopmentService trainingDevelopmentService;

	public TrainingController(
		IClubEventService eventService,
		IPlayerService playerService,
		ITrainingDevelopmentService trainingDevelopmentService)
	{
		this.eventService = eventService;
		this.playerService = playerService;
		this.trainingDevelopmentService = trainingDevelopmentService;
	}

	[HttpGet("metrics")]
	public ActionResult<IReadOnlyList<TrainingMetricDefinitionViewModel>> GetMetricDefinitions(
		[FromQuery] TrainingPlayerRole playerRole = TrainingPlayerRole.Outfield)
	{
		return Ok(trainingDevelopmentService
			.GetMetricDefinitions(playerRole)
			.Select(TrainingMetricDefinitionViewModel.FromDefinition)
			.ToList());
	}

	[HttpGet("events/{eventId}/assessments")]
	public async Task<ActionResult<IReadOnlyList<TrainingAssessmentViewModel>>> GetEventAssessments(
		string eventId,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(eventId, "Event", out var parsedEventId, out var errorResult))
		{
			return errorResult!;
		}

		var clubEvent = await eventService.GetByIdAsync(parsedEventId, cancellationToken);

		if (clubEvent is null || clubEvent.Type != ClubEventType.Training)
		{
			return NotFound();
		}

		var assessments = await trainingDevelopmentService.GetEventAssessmentsAsync(parsedEventId, cancellationToken);

		return Ok(assessments.Select(TrainingAssessmentViewModel.FromAssessment).ToList());
	}

	[HttpPut("events/{eventId}/assessments/{playerId}")]
	public async Task<ActionResult<TrainingAssessmentViewModel>> SaveAssessment(
		string eventId,
		string playerId,
		SaveTrainingAssessmentModel model,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(eventId, "Event", out var parsedEventId, out var eventErrorResult))
		{
			return eventErrorResult!;
		}

		if (!TryParseGuid(playerId, "Player", out var parsedPlayerId, out var playerErrorResult))
		{
			return playerErrorResult!;
		}

		var clubEvent = await eventService.GetByIdAsync(parsedEventId, cancellationToken);

		if (clubEvent is null || clubEvent.Type != ClubEventType.Training)
		{
			return NotFound();
		}

		var player = await playerService.GetByIdAsync(parsedPlayerId, cancellationToken);

		if (player is null)
		{
			return NotFound();
		}

		if (model.Metrics.Count == 0)
		{
			return BadRequest("At least one metric rating is required.");
		}

		var userIdResult = GetCurrentUserId();

		if (!userIdResult.Success)
		{
			return BadRequest(userIdResult.ErrorMessage);
		}

		var assessment = await trainingDevelopmentService.UpsertAsync(
			model.ToAssessment(parsedEventId, parsedPlayerId, userIdResult.UserId),
			cancellationToken);

		return Ok(TrainingAssessmentViewModel.FromAssessment(assessment));
	}

	[HttpGet("players/{playerId}/development")]
	public async Task<ActionResult<PlayerTrainingDevelopmentViewModel>> GetPlayerDevelopment(
		string playerId,
		[FromQuery] DateTime? from,
		[FromQuery] DateTime? to,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(playerId, "Player", out var parsedPlayerId, out var errorResult))
		{
			return errorResult!;
		}

		var player = await playerService.GetByIdAsync(parsedPlayerId, cancellationToken);

		if (player is null)
		{
			return NotFound();
		}

		var role = GetPlayerRole(player);
		var definitions = trainingDevelopmentService.GetMetricDefinitions(role);
		var assessments = await trainingDevelopmentService.GetPlayerAssessmentsAsync(
			parsedPlayerId,
			from,
			to,
			cancellationToken);

		return Ok(new PlayerTrainingDevelopmentViewModel
		{
			PlayerId = parsedPlayerId,
			PlayerRole = role,
			AssessmentCount = assessments.Count,
			LatestAssessment = assessments.FirstOrDefault() is { } latestAssessment
				? TrainingAssessmentViewModel.FromAssessment(latestAssessment)
				: null,
			Averages = BuildAverages(definitions, assessments),
			RecentAssessments = assessments
				.Take(8)
				.Select(TrainingAssessmentViewModel.FromAssessment)
				.ToList()
		});
	}

	private static TrainingPlayerRole GetPlayerRole(Player player)
	{
		return player.Positions.Any(position => position.Equals("GK", StringComparison.OrdinalIgnoreCase))
			? TrainingPlayerRole.Goalkeeper
			: TrainingPlayerRole.Outfield;
	}

	private static List<TrainingMetricAverageViewModel> BuildAverages(
		IReadOnlyList<TrainingMetricDefinition> definitions,
		IReadOnlyList<TrainingAssessment> assessments)
	{
		return definitions.Select(definition =>
		{
			var metricRatings = assessments
				.SelectMany(assessment => assessment.Metrics)
				.Where(metric => metric.Key == definition.Key)
				.ToList();

			return new TrainingMetricAverageViewModel
			{
				Key = definition.Key,
				Label = definition.Label,
				Rating = RoundAverage(metricRatings.Select(metric => metric.Rating)),
				Categories = definition.Categories.Select(category =>
				{
					var categoryRatings = metricRatings
						.SelectMany(metric => metric.Categories)
						.Where(item => item.Key == category.Key)
						.Select(item => item.Rating);

					return new TrainingMetricCategoryAverageViewModel
					{
						Key = category.Key,
						Label = category.Label,
						Rating = RoundAverage(categoryRatings)
					};
				}).ToList()
			};
		}).ToList();
	}

	private static double RoundAverage(IEnumerable<int> ratings)
	{
		var values = ratings.ToList();
		return values.Count == 0 ? 0 : Math.Round(values.Average(), 1, MidpointRounding.AwayFromZero);
	}

	private UserIdResult GetCurrentUserId()
	{
		var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

		if (string.IsNullOrWhiteSpace(userIdClaim))
		{
			return UserIdResult.Fail("User id claim is missing.");
		}

		if (!Guid.TryParse(userIdClaim, out var userId))
		{
			return UserIdResult.Fail("User id claim is invalid.");
		}

		return UserIdResult.Ok(userId);
	}

	private bool TryParseGuid(
		string id,
		string entityName,
		out Guid parsedId,
		out BadRequestObjectResult? errorResult)
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

	private sealed record UserIdResult(bool Success, Guid UserId, string ErrorMessage)
	{
		public static UserIdResult Ok(Guid userId) => new(true, userId, string.Empty);

		public static UserIdResult Fail(string errorMessage) => new(false, Guid.Empty, errorMessage);
	}
}
