using KingsManage;

namespace KingsManage.Web.Models;

public sealed class ClubFormViewModel
{
	public Guid Id { get; set; }
	public string GoCode { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public ClubFormStatus Status { get; set; }
	public ClubFormSourceType SourceType { get; set; }
	public Guid? SourceMatchId { get; set; }
	public string SourceMatchLabel { get; set; } = string.Empty;
	public Guid? AppliedMatchAwardPlayerId { get; set; }
	public string CreatedByUserEmail { get; set; } = string.Empty;
	public bool AllowAnonymousResponses { get; set; }
	public bool AllowMultipleSubmissions { get; set; }
	public List<ClubFormQuestionViewModel> Questions { get; set; } = [];
	public bool HasSubmitted { get; set; }
	public int SubmissionCount { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }

	public static ClubFormViewModel FromForm(
		ClubForm form,
		bool hasSubmitted,
		int submissionCount = 0,
		string sourceMatchLabel = "")
	{
		return new ClubFormViewModel
		{
			Id = form.Id,
			GoCode = form.GoCode,
			Title = form.Title,
			Description = form.Description,
			Status = form.Status,
			SourceType = form.SourceType,
			SourceMatchId = form.SourceMatchId,
			SourceMatchLabel = sourceMatchLabel,
			AppliedMatchAwardPlayerId = form.AppliedMatchAwardPlayerId,
			CreatedByUserEmail = form.CreatedByUserEmail,
			AllowAnonymousResponses = form.AllowAnonymousResponses,
			AllowMultipleSubmissions = form.AllowMultipleSubmissions,
			Questions = form.Questions.Select(ClubFormQuestionViewModel.FromQuestion).ToList(),
			HasSubmitted = hasSubmitted,
			SubmissionCount = submissionCount,
			CreatedAt = form.CreatedAt,
			UpdatedAt = form.UpdatedAt
		};
	}
}

public sealed class ClubFormQuestionViewModel
{
	public Guid Id { get; set; }
	public string Prompt { get; set; } = string.Empty;
	public ClubFormQuestionType Type { get; set; }
	public bool IsRequired { get; set; }
	public List<string> Options { get; set; } = [];
	public int MinRating { get; set; }
	public int MaxRating { get; set; }

	public static ClubFormQuestionViewModel FromQuestion(ClubFormQuestion question)
	{
		return new ClubFormQuestionViewModel
		{
			Id = question.Id,
			Prompt = question.Prompt,
			Type = question.Type,
			IsRequired = question.IsRequired,
			Options = question.Options,
			MinRating = question.MinRating,
			MaxRating = question.MaxRating
		};
	}

	public ClubFormQuestion ToQuestion()
	{
		return new ClubFormQuestion
		{
			Id = Id,
			Prompt = Prompt,
			Type = Type,
			IsRequired = IsRequired,
			Options = Options,
			MinRating = MinRating,
			MaxRating = MaxRating
		};
	}
}

public sealed class SaveClubFormModel
{
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public ClubFormStatus Status { get; set; } = ClubFormStatus.Draft;
	public ClubFormSourceType SourceType { get; set; } = ClubFormSourceType.General;
	public Guid? SourceMatchId { get; set; }
	public bool AllowAnonymousResponses { get; set; } = true;
	public bool AllowMultipleSubmissions { get; set; }
	public List<ClubFormQuestionViewModel> Questions { get; set; } = [];

	public ClubForm ToForm(Guid createdByUserId, string createdByUserEmail)
	{
		return new ClubForm
		{
			Title = Title,
			Description = Description,
			Status = Status,
			SourceType = SourceType,
			SourceMatchId = SourceMatchId,
			AllowAnonymousResponses = AllowAnonymousResponses,
			AllowMultipleSubmissions = AllowMultipleSubmissions,
			Questions = Questions.Select(question => question.ToQuestion()).ToList(),
			CreatedByUserId = createdByUserId,
			CreatedByUserEmail = createdByUserEmail
		};
	}

	public ClubForm ToForm(ClubForm existingForm)
	{
		return new ClubForm
		{
			Id = existingForm.Id,
			OrganizationId = existingForm.OrganizationId,
			ClubId = existingForm.ClubId,
			GoCode = existingForm.GoCode,
			Title = Title,
			Description = Description,
			Status = Status,
			SourceType = existingForm.SourceType,
			SourceMatchId = existingForm.SourceMatchId,
			AppliedMatchAwardPlayerId = existingForm.AppliedMatchAwardPlayerId,
			AppliedMatchAwardAt = existingForm.AppliedMatchAwardAt,
			AllowAnonymousResponses = AllowAnonymousResponses,
			AllowMultipleSubmissions = AllowMultipleSubmissions,
			Questions = Questions.Select(question => question.ToQuestion()).ToList(),
			CreatedByUserId = existingForm.CreatedByUserId,
			CreatedByUserEmail = existingForm.CreatedByUserEmail,
			CreatedAt = existingForm.CreatedAt
		};
	}
}

public sealed class SubmitClubFormModel
{
	public string AnonymousSubmissionKey { get; set; } = string.Empty;
	public List<ClubFormAnswerViewModel> Answers { get; set; } = [];
}

public sealed class CreateMatchAwardsFormModel
{
	public Guid MatchId { get; set; }
}

public sealed class UpdateClubFormStatusModel
{
	public ClubFormStatus Status { get; set; }
}

public sealed class ClubFormAnswerViewModel
{
	public Guid QuestionId { get; set; }
	public string TextValue { get; set; } = string.Empty;
	public List<string> SelectedOptions { get; set; } = [];
	public int? RatingValue { get; set; }
	public bool? BooleanValue { get; set; }

	public static ClubFormAnswerViewModel FromAnswer(ClubFormAnswer answer)
	{
		return new ClubFormAnswerViewModel
		{
			QuestionId = answer.QuestionId,
			TextValue = answer.TextValue,
			SelectedOptions = answer.SelectedOptions,
			RatingValue = answer.RatingValue,
			BooleanValue = answer.BooleanValue
		};
	}

	public ClubFormAnswer ToAnswer()
	{
		return new ClubFormAnswer
		{
			QuestionId = QuestionId,
			TextValue = TextValue,
			SelectedOptions = SelectedOptions,
			RatingValue = RatingValue,
			BooleanValue = BooleanValue
		};
	}
}

public sealed class ClubFormResultsViewModel
{
	public Guid FormId { get; set; }
	public string Title { get; set; } = string.Empty;
	public int SubmissionCount { get; set; }
	public List<ClubFormQuestionResultViewModel> Questions { get; set; } = [];
}

public sealed class ClubFormQuestionResultViewModel
{
	public Guid QuestionId { get; set; }
	public string Prompt { get; set; } = string.Empty;
	public ClubFormQuestionType Type { get; set; }
	public int ResponseCount { get; set; }
	public List<ClubFormOptionResultViewModel> Options { get; set; } = [];
	public double? AverageRating { get; set; }
	public List<string> TextResponses { get; set; } = [];
}

public sealed class ClubFormOptionResultViewModel
{
	public string Value { get; set; } = string.Empty;
	public int Count { get; set; }
}
