using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class ClubFormService : IClubFormService
{
	private readonly IMongoCollection<ClubForm> forms;
	private readonly IMongoCollection<ClubFormSubmission> submissions;
	private readonly TenantMongoScope tenant;

	static ClubFormService()
	{
		RegisterClassMap<ClubForm>();
		RegisterClassMap<ClubFormSubmission>();
		RegisterClassMap<ClubFormQuestion>();
		RegisterClassMap<ClubFormQuestionOption>();
		RegisterClassMap<ClubFormAnswer>();
	}

	public ClubFormService(MongoContext context, TenantMongoScope tenant)
	{
		forms = context.Database.GetCollection<ClubForm>("forms");
		submissions = context.Database.GetCollection<ClubFormSubmission>("formSubmissions");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<ClubForm>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		var forms = await this.forms
			.Find(tenant.Filter<ClubForm>())
			.SortByDescending(form => form.UpdatedAt)
			.ToListAsync(cancellationToken);

		return forms.Select(NormaliseFormFromStorage).ToList();
	}

	public async Task<ClubForm?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var form = await forms
			.Find(tenant.Filter<ClubForm>(form => form.Id == id))
			.FirstOrDefaultAsync(cancellationToken);

		return form is null ? null : NormaliseFormFromStorage(form);
	}

	public async Task<ClubForm?> GetByGoCodeAsync(string goCode, CancellationToken cancellationToken = default)
	{
		var normalisedCode = NormaliseGoCode(goCode);
		if (string.IsNullOrWhiteSpace(normalisedCode))
		{
			return null;
		}

		var form = await forms
			.Find(form => form.GoCode == normalisedCode)
			.FirstOrDefaultAsync(cancellationToken);

		return form is null ? null : NormaliseFormFromStorage(form);
	}

	public async Task<ClubForm?> GetMatchAwardsFormAsync(Guid matchId, CancellationToken cancellationToken = default)
	{
		if (matchId == Guid.Empty)
		{
			return null;
		}

		var form = await forms
			.Find(tenant.Filter<ClubForm>(form =>
				form.SourceType == ClubFormSourceType.MatchAwards &&
				form.SourceMatchId == matchId))
			.SortByDescending(form => form.CreatedAt)
			.FirstOrDefaultAsync(cancellationToken);

		return form is null ? null : NormaliseFormFromStorage(form);
	}

	public async Task<ClubForm> CreateAsync(ClubForm form, CancellationToken cancellationToken = default)
	{
		form.Id = form.Id == Guid.Empty ? Guid.NewGuid() : form.Id;
		PrepareFormForSave(form, true);
		tenant.Assign(form);

		await forms.InsertOneAsync(form, cancellationToken: cancellationToken);

		return form;
	}

	public async Task<ClubForm?> UpdateAsync(ClubForm form, CancellationToken cancellationToken = default)
	{
		PrepareFormForSave(form, false);
		tenant.Assign(form);

		var result = await forms.ReplaceOneAsync(
			tenant.Filter<ClubForm>(existingForm => existingForm.Id == form.Id),
			form,
			cancellationToken: cancellationToken);

		return result.MatchedCount == 0 ? null : form;
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var deleteForm = await forms.DeleteOneAsync(
			tenant.Filter<ClubForm>(form => form.Id == id),
			cancellationToken);

		if (deleteForm.DeletedCount == 0)
		{
			return false;
		}

		await submissions.DeleteManyAsync(
			tenant.Filter<ClubFormSubmission>(submission => submission.FormId == id),
			cancellationToken);

		return true;
	}

	public async Task<bool> HasSubmittedAsync(
		Guid formId,
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		return await HasSubmittedAsync(formId, BuildUserRespondentKey(userId), cancellationToken);
	}

	public async Task<bool> HasSubmittedAsync(
		Guid formId,
		string respondentKey,
		CancellationToken cancellationToken = default)
	{
		var normalisedRespondentKey = NormaliseRespondentKey(respondentKey);
		if (string.IsNullOrWhiteSpace(normalisedRespondentKey))
		{
			return false;
		}

		return await submissions
			.Find(tenant.Filter<ClubFormSubmission>(submission =>
				submission.FormId == formId &&
				submission.RespondentKey == normalisedRespondentKey))
			.AnyAsync(cancellationToken);
	}

	public async Task<bool> HasSubmittedAsync(
		ClubForm form,
		string respondentKey,
		CancellationToken cancellationToken = default)
	{
		var normalisedRespondentKey = NormaliseRespondentKey(respondentKey);
		if (string.IsNullOrWhiteSpace(normalisedRespondentKey))
		{
			return false;
		}

		return await submissions
			.Find(submission =>
				submission.OrganizationId == form.OrganizationId &&
				submission.ClubId == form.ClubId &&
				submission.FormId == form.Id &&
				submission.RespondentKey == normalisedRespondentKey)
			.AnyAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<ClubFormSubmission>> GetSubmissionsAsync(
		Guid formId,
		CancellationToken cancellationToken = default)
	{
		var submissions = await this.submissions
			.Find(tenant.Filter<ClubFormSubmission>(submission => submission.FormId == formId))
			.SortBy(submission => submission.SubmittedAt)
			.ToListAsync(cancellationToken);

		return submissions.Select(NormaliseSubmissionFromStorage).ToList();
	}

	public async Task<ClubFormSubmission> SubmitAsync(
		ClubFormSubmission submission,
		CancellationToken cancellationToken = default)
	{
		submission.Id = submission.Id == Guid.Empty ? Guid.NewGuid() : submission.Id;
		PrepareSubmissionForSave(submission);

		if (submission.OrganizationId == Guid.Empty || submission.ClubId == Guid.Empty)
		{
			tenant.Assign(submission);
		}

		try
		{
			await submissions.InsertOneAsync(submission, cancellationToken: cancellationToken);
		}
		catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
		{
			throw new InvalidOperationException("You have already submitted this form.", exception);
		}

		return submission;
	}

	private static void RegisterClassMap<T>()
	{
		if (BsonClassMap.IsClassMapRegistered(typeof(T)))
		{
			return;
		}

		BsonClassMap.RegisterClassMap<T>(
			classMap =>
			{
				classMap.AutoMap();
				classMap.SetIgnoreExtraElements(true);
			});
	}

	private static ClubForm NormaliseFormFromStorage(ClubForm form)
	{
		form.GoCode = NormaliseGoCode(form.GoCode);
		if (string.IsNullOrWhiteSpace(form.GoCode))
		{
			form.GoCode = GenerateGoCode();
		}

		form.Title ??= string.Empty;
		form.Description ??= string.Empty;
		form.CreatedByUserEmail ??= string.Empty;
		form.Questions = NormaliseQuestions(form.Questions);
		form.AppliedMatchAwardPlayerIds ??= [];

		if (form.CreatedAt == default)
		{
			form.CreatedAt = DateTime.UtcNow;
		}

		if (form.UpdatedAt == default)
		{
			form.UpdatedAt = form.CreatedAt;
		}

		return form;
	}

	private static ClubFormSubmission NormaliseSubmissionFromStorage(ClubFormSubmission submission)
	{
		submission.RespondentKey = NormaliseRespondentKey(submission.RespondentKey);
		submission.SubmissionLimitKey = NormaliseRespondentKey(submission.SubmissionLimitKey);
		submission.Answers ??= [];

		foreach (var answer in submission.Answers)
		{
			answer.TextValue ??= string.Empty;
			answer.SelectedOptions ??= [];
		}

		if (submission.SubmittedAt == default)
		{
			submission.SubmittedAt = DateTime.UtcNow;
		}

		return submission;
	}

	private static void PrepareFormForSave(ClubForm form, bool isNew)
	{
		form.Title = form.Title.Trim();
		form.Description = form.Description.Trim();
		form.CreatedByUserEmail = form.CreatedByUserEmail.Trim();
		form.AppliedMatchAwardPlayerIds ??= [];
		form.GoCode = NormaliseGoCode(form.GoCode);
		if (isNew || string.IsNullOrWhiteSpace(form.GoCode))
		{
			form.GoCode = GenerateGoCode();
		}
		form.Questions = NormaliseQuestions(form.Questions);

		if (isNew || form.CreatedAt == default)
		{
			form.CreatedAt = DateTime.UtcNow;
		}

		form.UpdatedAt = DateTime.UtcNow;
	}

	private static List<ClubFormQuestion> NormaliseQuestions(List<ClubFormQuestion>? questions)
	{
		return (questions ?? [])
			.Where(question => !string.IsNullOrWhiteSpace(question.Prompt))
			.Select(question =>
			{
				question.Id = question.Id == Guid.Empty ? Guid.NewGuid() : question.Id;
				question.Prompt = question.Prompt.Trim();
				question.Options = (question.Options ?? [])
					.Select(option => option.Trim())
					.Where(option => !string.IsNullOrWhiteSpace(option))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
				question.ChoiceOptions = NormaliseChoiceOptions(question.ChoiceOptions, question.Options);
				if (question.Options.Count == 0)
				{
					question.Options = question.ChoiceOptions
						.Select(option => option.Label)
						.Where(label => !string.IsNullOrWhiteSpace(label))
						.Distinct(StringComparer.OrdinalIgnoreCase)
						.ToList();
				}
				question.MinRating = Math.Max(1, question.MinRating);
				question.MaxRating = Math.Max(question.MinRating, Math.Min(10, question.MaxRating));
				return question;
			})
			.ToList();
	}

	private static List<ClubFormQuestionOption> NormaliseChoiceOptions(
		List<ClubFormQuestionOption>? choiceOptions,
		List<string> legacyOptions)
	{
		var normalised = (choiceOptions ?? [])
			.Select(option =>
			{
				option.Value = (option.Value ?? string.Empty).Trim();
				option.Label = (option.Label ?? string.Empty).Trim();
				if (string.IsNullOrWhiteSpace(option.Value) && option.PlayerId.HasValue)
				{
					option.Value = option.PlayerId.Value.ToString("D");
				}
				if (string.IsNullOrWhiteSpace(option.Label))
				{
					option.Label = option.Value;
				}
				return option;
			})
			.Where(option => !string.IsNullOrWhiteSpace(option.Value) && !string.IsNullOrWhiteSpace(option.Label))
			.GroupBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.ToList();

		if (normalised.Count > 0)
		{
			return normalised;
		}

		return legacyOptions
			.Select(option => new ClubFormQuestionOption
			{
				Value = option,
				Label = option
			})
			.ToList();
	}

	private static void PrepareSubmissionForSave(ClubFormSubmission submission)
	{
		submission.Answers ??= [];

		foreach (var answer in submission.Answers)
		{
			answer.TextValue = (answer.TextValue ?? string.Empty).Trim();
			answer.SelectedOptions = (answer.SelectedOptions ?? [])
				.Select(option => option.Trim())
				.Where(option => !string.IsNullOrWhiteSpace(option))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		submission.RespondentKey = NormaliseRespondentKey(submission.RespondentKey);
		submission.SubmissionLimitKey = NormaliseRespondentKey(submission.SubmissionLimitKey);

		if (submission.SubmittedAt == default)
		{
			submission.SubmittedAt = DateTime.UtcNow;
		}
	}

	private static string BuildUserRespondentKey(Guid userId) => $"user:{userId:N}";

	private static string NormaliseRespondentKey(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

	private static string NormaliseGoCode(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

	private static string GenerateGoCode() => Guid.NewGuid().ToString("N")[..10];
}
