using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class FormAnalyticsService : IFormAnalyticsService
{
	private readonly IMongoCollection<ClubForm> forms;
	private readonly IMongoCollection<ClubFormSubmission> submissions;
	private readonly IMongoCollection<FormAnalyticsSession> sessions;
	private readonly IMongoCollection<FormAnalyticsEvent> events;
	private readonly TenantMongoScope tenant;

	static FormAnalyticsService()
	{
		RegisterClassMap<FormAnalyticsSession>();
		RegisterClassMap<FormAnalyticsEvent>();
	}

	public FormAnalyticsService(MongoContext context, TenantMongoScope tenant)
	{
		forms = context.Database.GetCollection<ClubForm>("forms");
		submissions = context.Database.GetCollection<ClubFormSubmission>("formSubmissions");
		sessions = context.Database.GetCollection<FormAnalyticsSession>("formAnalyticsSessions");
		events = context.Database.GetCollection<FormAnalyticsEvent>("formAnalyticsEvents");
		this.tenant = tenant;
	}

	public async Task RecordViewAsync(ClubForm form, Guid sessionId, Guid? userId, CancellationToken cancellationToken = default)
	{
		ValidateSession(sessionId);
		var now = DateTime.UtcNow;
		var filter = SessionFilter(form, sessionId);
		var update = Builders<FormAnalyticsSession>.Update
			.SetOnInsert(item => item.Id, sessionId)
			.SetOnInsert(item => item.OrganizationId, form.OrganizationId)
			.SetOnInsert(item => item.ClubId, form.ClubId)
			.SetOnInsert(item => item.FormId, form.Id)
			.SetOnInsert(item => item.UserId, NormaliseUserId(userId))
			.SetOnInsert(item => item.StartedAt, now)
			.Set(item => item.LastActivityAt, now);
		try
		{
			await sessions.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
		}
		catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			// Two first events can arrive together. The unique session index means the
			// winner created the visit, so only refresh activity on the existing record.
			await sessions.UpdateOneAsync(filter, Builders<FormAnalyticsSession>.Update.Set(item => item.LastActivityAt, now), cancellationToken: cancellationToken);
		}
		await UpsertEventAsync(form, sessionId, FormAnalyticsEventType.Viewed, null, userId, string.Empty, cancellationToken);
	}

	public async Task RecordInteractionAsync(ClubForm form, Guid sessionId, Guid? userId, CancellationToken cancellationToken = default)
	{
		await EnsureSessionAsync(form, sessionId, userId, cancellationToken);
		await sessions.UpdateOneAsync(SessionFilter(form, sessionId), Builders<FormAnalyticsSession>.Update
			.Set(item => item.HasInteracted, true)
			.Set(item => item.LastActivityAt, DateTime.UtcNow), cancellationToken: cancellationToken);
		await UpsertEventAsync(form, sessionId, FormAnalyticsEventType.InteractionStarted, null, userId, string.Empty, cancellationToken);
	}

	public async Task RecordFieldInteractionAsync(ClubForm form, Guid sessionId, Guid fieldId, Guid? userId, CancellationToken cancellationToken = default)
	{
		if (fieldId == Guid.Empty || form.Questions.All(question => question.Id != fieldId)) return;
		await RecordInteractionAsync(form, sessionId, userId, cancellationToken);
		await UpsertEventAsync(form, sessionId, FormAnalyticsEventType.FieldInteracted, fieldId, userId, string.Empty, cancellationToken);
	}

	public async Task RecordValidationErrorAsync(ClubForm form, Guid sessionId, Guid? fieldId, string errorType, Guid? userId, CancellationToken cancellationToken = default)
	{
		await EnsureSessionAsync(form, sessionId, userId, cancellationToken);
		var safeFieldId = fieldId.HasValue && form.Questions.Any(question => question.Id == fieldId) ? fieldId : null;
		var analyticsEvent = BuildEvent(form, sessionId, FormAnalyticsEventType.ValidationError, safeFieldId, userId);
		analyticsEvent.ErrorType = string.IsNullOrWhiteSpace(errorType) ? "validation" : errorType.Trim()[..Math.Min(errorType.Trim().Length, 80)];
		await events.InsertOneAsync(analyticsEvent, cancellationToken: cancellationToken);
	}

	public async Task UpdateDurationAsync(ClubForm form, Guid sessionId, long engagedDurationMs, Guid? userId, CancellationToken cancellationToken = default)
	{
		await EnsureSessionAsync(form, sessionId, userId, cancellationToken);
		var duration = Math.Clamp(engagedDurationMs, 0, (long)TimeSpan.FromDays(1).TotalMilliseconds);
		await sessions.UpdateOneAsync(SessionFilter(form, sessionId), Builders<FormAnalyticsSession>.Update
			.Max(item => item.EngagedDurationMs, duration)
			.Set(item => item.LastActivityAt, DateTime.UtcNow), cancellationToken: cancellationToken);
	}

	public async Task RecordSubmissionAsync(ClubForm form, Guid sessionId, Guid? userId, CancellationToken cancellationToken = default)
	{
		await EnsureSessionAsync(form, sessionId, userId, cancellationToken);
		var now = DateTime.UtcNow;
		await sessions.UpdateOneAsync(SessionFilter(form, sessionId), Builders<FormAnalyticsSession>.Update
			.Set(item => item.HasInteracted, true)
			.Set(item => item.SubmittedAt, now)
			.Set(item => item.LastActivityAt, now), cancellationToken: cancellationToken);
		await UpsertEventAsync(form, sessionId, FormAnalyticsEventType.Submitted, null, userId, string.Empty, cancellationToken);
	}

	public async Task<FormAnalyticsOverview> GetOverviewAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
	{
		var formList = await forms.Find(tenant.Filter<ClubForm>()).ToListAsync(cancellationToken);
		var sessionList = await sessions.Find(DateFilter(tenant.Filter<FormAnalyticsSession>(), item => item.StartedAt, from, to)).ToListAsync(cancellationToken);
		var submissionList = await submissions.Find(DateFilter(tenant.Filter<ClubFormSubmission>(), item => item.SubmittedAt, from, to)).ToListAsync(cancellationToken);
		var eventCounts = await GetEventCountsAsync(
			DateFilter(tenant.Filter<FormAnalyticsEvent>(), item => item.OccurredAt, from, to),
			cancellationToken);
		return FormAnalyticsReportBuilder.BuildOverview(formList, sessionList, submissionList, eventCounts, from, to);
	}

	public async Task<FormAnalyticsDetail?> GetFormAnalyticsAsync(Guid formId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
	{
		var form = await forms.Find(tenant.Filter<ClubForm>(item => item.Id == formId)).FirstOrDefaultAsync(cancellationToken);
		if (form is null) return null;
		var sessionList = await sessions.Find(DateFilter(tenant.Filter<FormAnalyticsSession>(item => item.FormId == formId), item => item.StartedAt, from, to)).ToListAsync(cancellationToken);
		var submissionList = await submissions.Find(DateFilter(tenant.Filter<ClubFormSubmission>(item => item.FormId == formId), item => item.SubmittedAt, from, to)).ToListAsync(cancellationToken);
		var eventCounts = await GetEventCountsAsync(
			DateFilter(tenant.Filter<FormAnalyticsEvent>(item => item.FormId == formId), item => item.OccurredAt, from, to),
			cancellationToken);
		return FormAnalyticsReportBuilder.BuildDetail(form, sessionList, submissionList, eventCounts, from, to);
	}

	public async Task DeleteForFormAsync(Guid formId, CancellationToken cancellationToken = default)
	{
		await sessions.DeleteManyAsync(tenant.Filter<FormAnalyticsSession>(item => item.FormId == formId), cancellationToken);
		await events.DeleteManyAsync(tenant.Filter<FormAnalyticsEvent>(item => item.FormId == formId), cancellationToken);
	}

	private Task EnsureSessionAsync(ClubForm form, Guid sessionId, Guid? userId, CancellationToken cancellationToken) =>
		RecordViewAsync(form, sessionId, userId, cancellationToken);

	private Task<List<FormAnalyticsEventCount>> GetEventCountsAsync(
		FilterDefinition<FormAnalyticsEvent> filter,
		CancellationToken cancellationToken) => events
			.Aggregate()
			.Match(filter)
			.Group(
				item => new { item.FormId, item.EventType, item.FieldId },
				group => new FormAnalyticsEventCount
				{
					FormId = group.Key.FormId,
					EventType = group.Key.EventType,
					FieldId = group.Key.FieldId,
					Count = group.Count()
				})
			.ToListAsync(cancellationToken);

	private async Task UpsertEventAsync(ClubForm form, Guid sessionId, FormAnalyticsEventType eventType, Guid? fieldId, Guid? userId, string errorType, CancellationToken cancellationToken)
	{
		var filter = Builders<FormAnalyticsEvent>.Filter.Eq(item => item.OrganizationId, form.OrganizationId) &
			Builders<FormAnalyticsEvent>.Filter.Eq(item => item.ClubId, form.ClubId) &
			Builders<FormAnalyticsEvent>.Filter.Eq(item => item.FormId, form.Id) &
			Builders<FormAnalyticsEvent>.Filter.Eq(item => item.SessionId, sessionId) &
			Builders<FormAnalyticsEvent>.Filter.Eq(item => item.EventType, eventType) &
			Builders<FormAnalyticsEvent>.Filter.Eq(item => item.FieldId, fieldId);
		var analyticsEvent = BuildEvent(form, sessionId, eventType, fieldId, userId);
		analyticsEvent.ErrorType = errorType;
		try
		{
			await events.UpdateOneAsync(
				filter,
				Builders<FormAnalyticsEvent>.Update
					.SetOnInsert(item => item.Id, analyticsEvent.Id)
					.SetOnInsert(item => item.OrganizationId, analyticsEvent.OrganizationId)
					.SetOnInsert(item => item.ClubId, analyticsEvent.ClubId)
					.SetOnInsert(item => item.FormId, analyticsEvent.FormId)
					.SetOnInsert(item => item.SessionId, analyticsEvent.SessionId)
					.SetOnInsert(item => item.UserId, analyticsEvent.UserId)
					.SetOnInsert(item => item.EventType, analyticsEvent.EventType)
					.SetOnInsert(item => item.FieldId, analyticsEvent.FieldId)
					.SetOnInsert(item => item.ErrorType, analyticsEvent.ErrorType)
					.SetOnInsert(item => item.OccurredAt, analyticsEvent.OccurredAt),
				new UpdateOptions { IsUpsert = true },
				cancellationToken);
		}
		catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			// The event-level unique indexes make tracking retries idempotent.
		}
	}

	private static FormAnalyticsEvent BuildEvent(ClubForm form, Guid sessionId, FormAnalyticsEventType eventType, Guid? fieldId, Guid? userId) => new()
	{
		Id = Guid.NewGuid(), OrganizationId = form.OrganizationId, ClubId = form.ClubId, FormId = form.Id,
		SessionId = sessionId, UserId = NormaliseUserId(userId), EventType = eventType, FieldId = fieldId,
		OccurredAt = DateTime.UtcNow
	};

	private static FilterDefinition<FormAnalyticsSession> SessionFilter(ClubForm form, Guid sessionId) =>
		Builders<FormAnalyticsSession>.Filter.Eq(item => item.OrganizationId, form.OrganizationId) &
		Builders<FormAnalyticsSession>.Filter.Eq(item => item.ClubId, form.ClubId) &
		Builders<FormAnalyticsSession>.Filter.Eq(item => item.FormId, form.Id) &
		Builders<FormAnalyticsSession>.Filter.Eq(item => item.Id, sessionId);

	private static FilterDefinition<T> DateFilter<T>(FilterDefinition<T> filter, System.Linq.Expressions.Expression<Func<T, DateTime>> field, DateTime? from, DateTime? to)
	{
		if (from.HasValue) filter &= Builders<T>.Filter.Gte(field, from.Value.ToUniversalTime());
		if (to.HasValue) filter &= Builders<T>.Filter.Lt(field, to.Value.ToUniversalTime());
		return filter;
	}

	private static Guid? NormaliseUserId(Guid? userId) => userId == Guid.Empty ? null : userId;
	private static void ValidateSession(Guid sessionId)
	{
		if (sessionId == Guid.Empty) throw new ArgumentException("Analytics session id is required.", nameof(sessionId));
	}
	private static void RegisterClassMap<T>()
	{
		if (BsonClassMap.IsClassMapRegistered(typeof(T))) return;
		BsonClassMap.RegisterClassMap<T>(map => { map.AutoMap(); map.SetIgnoreExtraElements(true); });
	}
}
