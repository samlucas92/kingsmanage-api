using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/forms")]
public sealed class FormsController : ControllerBase
{
	private readonly IClubFormService formService;
	private readonly IMatchService matchService;
	private readonly IPlayerService playerService;

	public FormsController(
		IClubFormService formService,
		IMatchService matchService,
		IPlayerService playerService)
	{
		this.formService = formService;
		this.matchService = matchService;
		this.playerService = playerService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<ClubFormViewModel>>> GetAll(CancellationToken cancellationToken)
	{
		var userIdResult = GetCurrentUserId();
		if (!userIdResult.Success) return BadRequest(userIdResult.ErrorMessage);

		var forms = await formService.GetAllAsync(cancellationToken);
		var canManage = CanManageForms();
		var visibleForms = canManage ? forms : forms.Where(form => form.Status == ClubFormStatus.Open);
		var viewModels = new List<ClubFormViewModel>();

		foreach (var form in visibleForms)
		{
			var hasSubmitted = await formService.HasSubmittedAsync(form.Id, userIdResult.UserId, cancellationToken);
			var submissionCount = canManage
				? (await formService.GetSubmissionsAsync(form.Id, cancellationToken)).Count
				: 0;
			viewModels.Add(ClubFormViewModel.FromForm(form, hasSubmitted, submissionCount));
		}

		return Ok(viewModels);
	}

	[HttpGet("{id}")]
	public async Task<ActionResult<ClubFormViewModel>> GetById(string id, CancellationToken cancellationToken)
	{
		if (!TryParseGuid(id, "Form", out var formId, out var errorResult)) return errorResult!;

		var userIdResult = GetCurrentUserId();
		if (!userIdResult.Success) return BadRequest(userIdResult.ErrorMessage);

		var form = await formService.GetByIdAsync(formId, cancellationToken);
		if (form is null) return NotFound();
		if (form.Status != ClubFormStatus.Open && !CanManageForms()) return NotFound();

		var hasSubmitted = await formService.HasSubmittedAsync(form.Id, userIdResult.UserId, cancellationToken);
		var submissionCount = CanManageForms()
			? (await formService.GetSubmissionsAsync(form.Id, cancellationToken)).Count
			: 0;

		return Ok(ClubFormViewModel.FromForm(form, hasSubmitted, submissionCount));
	}

	[AllowAnonymous]
	[HttpGet("go/{goCode}")]
	public async Task<ActionResult<ClubFormViewModel>> GetByGoCode(
		string goCode,
		[FromQuery] string anonymousSubmissionKey,
		CancellationToken cancellationToken)
	{
		var form = await formService.GetByGoCodeAsync(goCode, cancellationToken);
		if (form is null || form.Status != ClubFormStatus.Open) return NotFound();

		var userId = TryGetCurrentUserId();
		if (!form.AllowAnonymousResponses && userId is null)
		{
			return Unauthorized("Please sign in to answer this form.");
		}

		var respondentKey = BuildRespondentKey(userId, anonymousSubmissionKey);
		var hasSubmitted = await formService.HasSubmittedAsync(form, respondentKey, cancellationToken);

		return Ok(ClubFormViewModel.FromForm(form, hasSubmitted));
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpPost]
	public async Task<ActionResult<ClubFormViewModel>> Create(
		SaveClubFormModel model,
		CancellationToken cancellationToken)
	{
		var validationError = ValidateForm(model);
		if (validationError is not null) return BadRequest(validationError);

		var userIdResult = GetCurrentUserId();
		if (!userIdResult.Success) return BadRequest(userIdResult.ErrorMessage);

		var form = model.ToForm(
			userIdResult.UserId,
			User.FindFirstValue(ClaimTypes.Email) ?? string.Empty);
		var created = await formService.CreateAsync(form, cancellationToken);

		return CreatedAtAction(
			nameof(GetById),
			new { id = created.Id },
			ClubFormViewModel.FromForm(created, false));
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpPost("match-awards")]
	public async Task<ActionResult<ClubFormViewModel>> CreateMatchAwardsForm(
		CreateMatchAwardsFormModel model,
		CancellationToken cancellationToken)
	{
		if (model.MatchId == Guid.Empty)
		{
			return BadRequest("Match id is required.");
		}

		var match = await matchService.GetByIdAsync(model.MatchId, cancellationToken);
		if (match is null)
		{
			return NotFound("Match not found.");
		}

		var playerOptions = await BuildMatchAwardPlayerOptionsAsync(match, cancellationToken);
		if (playerOptions.Count == 0)
		{
			return BadRequest("Select at least one player before creating an awards form.");
		}

		var userIdResult = GetCurrentUserId();
		if (!userIdResult.Success) return BadRequest(userIdResult.ErrorMessage);

		var form = new ClubForm
		{
			Title = $"Match awards: {match.Opponent}",
			Description = $"Vote for the match awards from {match.Date:dd/MM/yyyy}.",
			Status = ClubFormStatus.Open,
			AllowAnonymousResponses = true,
			AllowMultipleSubmissions = false,
			CreatedByUserId = userIdResult.UserId,
			CreatedByUserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
			Questions =
			[
				new ClubFormQuestion
				{
					Prompt = "Man of the match",
					Type = ClubFormQuestionType.SingleChoice,
					IsRequired = true,
					Options = playerOptions
				},
				new ClubFormQuestion
				{
					Prompt = "Dick of the day",
					Type = ClubFormQuestionType.SingleChoice,
					IsRequired = true,
					Options = [..playerOptions, "Other"]
				},
				new ClubFormQuestion
				{
					Prompt = "Dick of the day reason — if Other, please specify name",
					Type = ClubFormQuestionType.LongText,
					IsRequired = false
				},
				new ClubFormQuestion
				{
					Prompt = "Quote/moment of the day",
					Type = ClubFormQuestionType.LongText,
					IsRequired = false
				}
			]
		};

		var created = await formService.CreateAsync(form, cancellationToken);

		return CreatedAtAction(
			nameof(GetById),
			new { id = created.Id },
			ClubFormViewModel.FromForm(created, false));
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpPut("{id}")]
	public async Task<ActionResult<ClubFormViewModel>> Update(
		string id,
		SaveClubFormModel model,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(id, "Form", out var formId, out var errorResult)) return errorResult!;

		var validationError = ValidateForm(model);
		if (validationError is not null) return BadRequest(validationError);

		var existing = await formService.GetByIdAsync(formId, cancellationToken);
		if (existing is null) return NotFound();

		var updated = await formService.UpdateAsync(model.ToForm(existing), cancellationToken);
		return updated is null
			? NotFound()
			: Ok(ClubFormViewModel.FromForm(updated, false));
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpDelete("{id}")]
	public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
	{
		if (!TryParseGuid(id, "Form", out var formId, out var errorResult)) return errorResult!;

		var deleted = await formService.DeleteAsync(formId, cancellationToken);
		return deleted ? NoContent() : NotFound();
	}

	[HttpPost("{id}/submissions")]
	public async Task<ActionResult<ClubFormViewModel>> Submit(
		string id,
		SubmitClubFormModel model,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(id, "Form", out var formId, out var errorResult)) return errorResult!;

		var userIdResult = GetCurrentUserId();
		if (!userIdResult.Success) return BadRequest(userIdResult.ErrorMessage);

		var form = await formService.GetByIdAsync(formId, cancellationToken);
		if (form is null || form.Status != ClubFormStatus.Open) return NotFound();

		var respondentKey = BuildUserRespondentKey(userIdResult.UserId);
		if (!form.AllowMultipleSubmissions &&
			await formService.HasSubmittedAsync(form.Id, respondentKey, cancellationToken))
		{
			return Conflict("You have already submitted this form.");
		}

		var answers = model.Answers.Select(answer => answer.ToAnswer()).ToList();
		var validationError = ValidateAnswers(form, answers);
		if (validationError is not null) return BadRequest(validationError);

		try
		{
			await formService.SubmitAsync(
				new ClubFormSubmission
				{
					FormId = form.Id,
					SubmittedByUserId = userIdResult.UserId,
					RespondentKey = respondentKey,
					SubmissionLimitKey = BuildSubmissionLimitKey(form, respondentKey),
					Answers = answers
				},
				cancellationToken);
		}
		catch (InvalidOperationException exception)
		{
			return Conflict(exception.Message);
		}

		return Ok(ClubFormViewModel.FromForm(form, true));
	}

	[AllowAnonymous]
	[HttpPost("go/{goCode}/submissions")]
	public async Task<ActionResult<ClubFormViewModel>> SubmitByGoCode(
		string goCode,
		SubmitClubFormModel model,
		CancellationToken cancellationToken)
	{
		var form = await formService.GetByGoCodeAsync(goCode, cancellationToken);
		if (form is null || form.Status != ClubFormStatus.Open) return NotFound();

		var userId = TryGetCurrentUserId();
		if (!form.AllowAnonymousResponses && userId is null)
		{
			return Unauthorized("Please sign in to answer this form.");
		}

		var respondentKey = BuildRespondentKey(userId, model.AnonymousSubmissionKey);
		if (string.IsNullOrWhiteSpace(respondentKey))
		{
			return BadRequest("Submission key is required.");
		}

		if (!form.AllowMultipleSubmissions &&
			await formService.HasSubmittedAsync(form, respondentKey, cancellationToken))
		{
			return Conflict("You have already submitted this form.");
		}

		var answers = model.Answers.Select(answer => answer.ToAnswer()).ToList();
		var validationError = ValidateAnswers(form, answers);
		if (validationError is not null) return BadRequest(validationError);

		try
		{
			await formService.SubmitAsync(
				new ClubFormSubmission
				{
					OrganizationId = form.OrganizationId,
					ClubId = form.ClubId,
					FormId = form.Id,
					SubmittedByUserId = userId ?? Guid.Empty,
					RespondentKey = respondentKey,
					SubmissionLimitKey = BuildSubmissionLimitKey(form, respondentKey),
					Answers = answers
				},
				cancellationToken);
		}
		catch (InvalidOperationException exception)
		{
			return Conflict(exception.Message);
		}

		return Ok(ClubFormViewModel.FromForm(form, true));
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpGet("{id}/results")]
	public async Task<ActionResult<ClubFormResultsViewModel>> GetResults(
		string id,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(id, "Form", out var formId, out var errorResult)) return errorResult!;

		var form = await formService.GetByIdAsync(formId, cancellationToken);
		if (form is null) return NotFound();

		var submissions = await formService.GetSubmissionsAsync(form.Id, cancellationToken);
		return Ok(BuildResults(form, submissions));
	}

	private bool CanManageForms() =>
		User.IsInRole(UserRole.Admin.ToString()) ||
		User.IsInRole(UserRole.Coach.ToString());

	private static string? ValidateForm(SaveClubFormModel model)
	{
		if (string.IsNullOrWhiteSpace(model.Title)) return "Form title is required.";
		if (model.Title.Trim().Length > 120) return "Form title must be 120 characters or fewer.";
		if (model.Questions.Count == 0) return "Add at least one question.";
		if (model.Questions.Count > 40) return "Forms are limited to 40 questions.";

		foreach (var question in model.Questions)
		{
			if (string.IsNullOrWhiteSpace(question.Prompt)) return "Every question needs prompt text.";
			if (question.Prompt.Trim().Length > 240) return "Question prompts must be 240 characters or fewer.";
			if (question.Type is ClubFormQuestionType.SingleChoice or ClubFormQuestionType.MultipleChoice &&
				question.Options.Count(option => !string.IsNullOrWhiteSpace(option)) < 2)
			{
				return "Choice questions need at least two options.";
			}
		}

		return null;
	}

	private static string? ValidateAnswers(ClubForm form, List<ClubFormAnswer> answers)
	{
		var answerLookup = answers
			.GroupBy(answer => answer.QuestionId)
			.ToDictionary(group => group.Key, group => group.First());

		foreach (var question in form.Questions)
		{
			answerLookup.TryGetValue(question.Id, out var answer);

			if (question.IsRequired && !HasAnswer(question, answer))
			{
				return $"Answer required: {question.Prompt}";
			}

			if (answer is null) continue;

			if (question.Type is ClubFormQuestionType.SingleChoice or ClubFormQuestionType.MultipleChoice)
			{
				var allowed = question.Options.ToHashSet(StringComparer.OrdinalIgnoreCase);
				if (answer.SelectedOptions.Any(option => !allowed.Contains(option)))
				{
					return $"Invalid option for: {question.Prompt}";
				}
				if (question.Type == ClubFormQuestionType.SingleChoice && answer.SelectedOptions.Count > 1)
				{
					return $"Choose one option for: {question.Prompt}";
				}
			}

			if (question.Type == ClubFormQuestionType.Rating &&
				answer.RatingValue.HasValue &&
				(answer.RatingValue < question.MinRating || answer.RatingValue > question.MaxRating))
			{
				return $"Rating out of range for: {question.Prompt}";
			}
		}

		return null;
	}

	private async Task<List<string>> BuildMatchAwardPlayerOptionsAsync(
		Match match,
		CancellationToken cancellationToken)
	{
		var playedPlayerIds = GetPlayedPlayerIds(match);
		if (playedPlayerIds.Count == 0)
		{
			return [];
		}

		var players = await playerService.GetAllAsync(cancellationToken);
		var playerNameLookup = players.ToDictionary(player => player.Id, player => player.Name);

		return playedPlayerIds
			.Select(playerId => playerNameLookup.TryGetValue(playerId, out var name) ? name : string.Empty)
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(name => name)
			.ToList();
	}

	private static List<Guid> GetPlayedPlayerIds(Match match)
	{
		var selectedPlayerIds = match.SelectedPlayers
			.Select(selectedPlayer => selectedPlayer.PlayerId)
			.Where(playerId => playerId != Guid.Empty)
			.Distinct()
			.ToList();

		if (match.PlayerStats.Count == 0)
		{
			return selectedPlayerIds;
		}

		var unusedPlayerIds = match.PlayerStats
			.Where(stats => stats.AppearanceType == MatchAppearanceType.UnusedSubstitute)
			.Select(stats => stats.PlayerId)
			.ToHashSet();

		return selectedPlayerIds
			.Where(playerId => !unusedPlayerIds.Contains(playerId))
			.ToList();
	}

	private static bool HasAnswer(ClubFormQuestion question, ClubFormAnswer? answer)
	{
		if (answer is null) return false;

		return question.Type switch
		{
			ClubFormQuestionType.ShortText or ClubFormQuestionType.LongText => !string.IsNullOrWhiteSpace(answer.TextValue),
			ClubFormQuestionType.SingleChoice or ClubFormQuestionType.MultipleChoice => answer.SelectedOptions.Count > 0,
			ClubFormQuestionType.Rating => answer.RatingValue.HasValue,
			ClubFormQuestionType.YesNo => answer.BooleanValue.HasValue,
			_ => false
		};
	}

	private static ClubFormResultsViewModel BuildResults(
		ClubForm form,
		IReadOnlyList<ClubFormSubmission> submissions)
	{
		return new ClubFormResultsViewModel
		{
			FormId = form.Id,
			Title = form.Title,
			SubmissionCount = submissions.Count,
			Questions = form.Questions.Select(question => BuildQuestionResult(question, submissions)).ToList()
		};
	}

	private static ClubFormQuestionResultViewModel BuildQuestionResult(
		ClubFormQuestion question,
		IReadOnlyList<ClubFormSubmission> submissions)
	{
		var answers = submissions
			.Select(submission => submission.Answers.FirstOrDefault(answer => answer.QuestionId == question.Id))
			.Where(answer => answer is not null)
			.Cast<ClubFormAnswer>()
			.ToList();

		var result = new ClubFormQuestionResultViewModel
		{
			QuestionId = question.Id,
			Prompt = question.Prompt,
			Type = question.Type,
			ResponseCount = answers.Count(HasVisibleAnswer)
		};

		if (question.Type is ClubFormQuestionType.SingleChoice or ClubFormQuestionType.MultipleChoice)
		{
			result.Options = question.Options
				.Select(option => new ClubFormOptionResultViewModel
				{
					Value = option,
					Count = answers.Count(answer => answer.SelectedOptions.Contains(option, StringComparer.OrdinalIgnoreCase))
				})
				.ToList();
		}
		else if (question.Type == ClubFormQuestionType.YesNo)
		{
			result.Options =
			[
				new ClubFormOptionResultViewModel { Value = "Yes", Count = answers.Count(answer => answer.BooleanValue == true) },
				new ClubFormOptionResultViewModel { Value = "No", Count = answers.Count(answer => answer.BooleanValue == false) }
			];
		}
		else if (question.Type == ClubFormQuestionType.Rating)
		{
			var ratingAnswers = answers
				.Where(answer => answer.RatingValue.HasValue)
				.Select(answer => answer.RatingValue!.Value)
				.ToList();
			result.AverageRating = ratingAnswers.Count == 0 ? null : Math.Round(ratingAnswers.Average(), 2);
			result.Options = Enumerable.Range(question.MinRating, question.MaxRating - question.MinRating + 1)
				.Select(rating => new ClubFormOptionResultViewModel
				{
					Value = rating.ToString(),
					Count = ratingAnswers.Count(value => value == rating)
				})
				.ToList();
		}
		else
		{
			result.TextResponses = answers
				.Select(answer => answer.TextValue.Trim())
				.Where(value => !string.IsNullOrWhiteSpace(value))
				.ToList();
		}

		return result;
	}

	private static bool HasVisibleAnswer(ClubFormAnswer answer) =>
		!string.IsNullOrWhiteSpace(answer.TextValue) ||
		answer.SelectedOptions.Count > 0 ||
		answer.RatingValue.HasValue ||
		answer.BooleanValue.HasValue;

	private static string BuildRespondentKey(Guid? userId, string anonymousSubmissionKey)
	{
		if (userId.HasValue)
		{
			return BuildUserRespondentKey(userId.Value);
		}

		var key = (anonymousSubmissionKey ?? string.Empty).Trim().ToLowerInvariant();
		return string.IsNullOrWhiteSpace(key) ? string.Empty : $"anon:{key}";
	}

	private static string BuildUserRespondentKey(Guid userId) => $"user:{userId:N}";

	private static string BuildSubmissionLimitKey(ClubForm form, string respondentKey) =>
		form.AllowMultipleSubmissions ? $"submission:{Guid.NewGuid():N}" : respondentKey;

	private Guid? TryGetCurrentUserId()
	{
		var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
		return Guid.TryParse(value, out var userId) ? userId : null;
	}

	private (bool Success, Guid UserId, string ErrorMessage) GetCurrentUserId()
	{
		var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

		return Guid.TryParse(value, out var userId)
			? (true, userId, string.Empty)
			: (false, Guid.Empty, "User id claim is missing.");
	}

	private static bool TryParseGuid(
		string value,
		string label,
		out Guid id,
		out ActionResult? errorResult)
	{
		if (Guid.TryParse(value, out id))
		{
			errorResult = null;
			return true;
		}

		errorResult = new BadRequestObjectResult($"{label} id is invalid.");
		return false;
	}
}
