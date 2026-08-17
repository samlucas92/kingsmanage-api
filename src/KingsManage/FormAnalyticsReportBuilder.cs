namespace KingsManage;

public static class FormAnalyticsReportBuilder
{
	public static FormAnalyticsOverview BuildOverview(
		IReadOnlyList<ClubForm> forms,
		IReadOnlyList<FormAnalyticsSession> sessions,
		IReadOnlyList<ClubFormSubmission> submissions,
		IReadOnlyList<FormAnalyticsEventCount> eventCounts,
		DateTime? from,
		DateTime? to)
	{
		var performances = forms
			.Select(form => BuildPerformance(form, sessions, submissions, eventCounts))
			.OrderByDescending(item => item.Views)
			.ThenBy(item => item.FormName)
			.ToList();
		var allVisitorKeys = sessions.Select(GetVisitorKey).Distinct().Count();
		var starts = sessions.Count(session => session.HasInteracted);
		var submissionCount = submissions.Count;

		return new FormAnalyticsOverview
		{
			From = from,
			To = to,
			TotalViews = sessions.Count,
			UniqueVisitors = allVisitorKeys,
			Starts = starts,
			Submissions = submissionCount,
			CompletionRate = Percentage(submissionCount, starts),
			ViewConversionRate = Percentage(submissionCount, sessions.Count),
			Forms = performances,
			NeedsAttention = performances
				.Where(item =>
					(item.Starts >= 10 && item.CompletionRate < 60) ||
					(item.Views >= 20 && Percentage(item.Starts, item.Views) < 25) ||
					item.ValidationErrors >= 10)
				.OrderByDescending(item => item.AbandonmentRate)
				.ThenByDescending(item => item.ValidationErrors)
				.Take(5)
				.ToList()
		};
	}

	public static FormAnalyticsDetail BuildDetail(
		ClubForm form,
		IReadOnlyList<FormAnalyticsSession> sessions,
		IReadOnlyList<ClubFormSubmission> submissions,
		IReadOnlyList<FormAnalyticsEventCount> eventCounts,
		DateTime? from,
		DateTime? to)
	{
		var performance = BuildPerformance(form, sessions, submissions, eventCounts);
		var completedSessionIds = submissions
			.Where(item => item.AnalyticsSessionId.HasValue)
			.Select(item => item.AnalyticsSessionId!.Value)
			.ToHashSet();
		var completedDurations = sessions
			.Where(item => item.SubmittedAt.HasValue || completedSessionIds.Contains(item.Id))
			.Select(item => item.EngagedDurationMs)
			.Where(value => value > 0)
			.ToList();
		var abandonedDurations = sessions
			.Where(item => item.HasInteracted && !item.SubmittedAt.HasValue && !completedSessionIds.Contains(item.Id))
			.Select(item => item.EngagedDurationMs)
			.Where(value => value > 0)
			.ToList();
		var durations = sessions.Select(item => item.EngagedDurationMs).Where(value => value > 0).Order().ToList();
		var trendStart = from ?? sessions.Select(item => (DateTime?)item.StartedAt).Min() ?? DateTime.UtcNow.Date;
		var trendEnd = to ?? DateTime.UtcNow;
		var useWeeklyBuckets = (trendEnd - trendStart).TotalDays > 45;

		return new FormAnalyticsDetail
		{
			FormId = performance.FormId,
			FormName = performance.FormName,
			Views = performance.Views,
			UniqueVisitors = performance.UniqueVisitors,
			Starts = performance.Starts,
			Submissions = performance.Submissions,
			Abandoned = performance.Abandoned,
			CompletionRate = performance.CompletionRate,
			AbandonmentRate = performance.AbandonmentRate,
			ViewConversionRate = performance.ViewConversionRate,
			ViewToStartRate = Percentage(performance.Starts, performance.Views),
			AverageEngagedDurationMs = performance.AverageEngagedDurationMs,
			MedianEngagedDurationMs = Median(durations),
			AverageCompletedDurationMs = Average(completedDurations),
			AverageAbandonedDurationMs = Average(abandonedDurations),
			ValidationErrors = performance.ValidationErrors,
			Trends = BuildTrends(sessions, submissions, useWeeklyBuckets),
			Fields = form.Questions.Select(question => new FormFieldAnalytics
			{
				FieldId = question.Id,
				FieldName = question.Prompt,
				IsRequired = question.IsRequired,
				Interactions = EventCount(eventCounts, form.Id, FormAnalyticsEventType.FieldInteracted, question.Id),
				ValidationErrors = EventCount(eventCounts, form.Id, FormAnalyticsEventType.ValidationError, question.Id)
			}).ToList()
		};
	}

	private static FormAnalyticsPerformance BuildPerformance(
		ClubForm form,
		IReadOnlyList<FormAnalyticsSession> allSessions,
		IReadOnlyList<ClubFormSubmission> allSubmissions,
		IReadOnlyList<FormAnalyticsEventCount> eventCounts)
	{
		var sessions = allSessions.Where(item => item.FormId == form.Id).ToList();
		var submissions = allSubmissions.Where(item => item.FormId == form.Id).ToList();
		var completedSessionIds = submissions
			.Where(item => item.AnalyticsSessionId.HasValue)
			.Select(item => item.AnalyticsSessionId!.Value)
			.ToHashSet();
		var starts = sessions.Count(item => item.HasInteracted);
		var abandoned = sessions.Count(item => item.HasInteracted && !item.SubmittedAt.HasValue && !completedSessionIds.Contains(item.Id));

		return new FormAnalyticsPerformance
		{
			FormId = form.Id,
			FormName = form.Title,
			Views = sessions.Count,
			UniqueVisitors = sessions.Select(GetVisitorKey).Distinct().Count(),
			Starts = starts,
			Submissions = submissions.Count,
			Abandoned = abandoned,
			CompletionRate = Percentage(submissions.Count, starts),
			AbandonmentRate = Percentage(abandoned, starts),
			ViewConversionRate = Percentage(submissions.Count, sessions.Count),
			AverageEngagedDurationMs = Average(sessions.Select(item => item.EngagedDurationMs).Where(value => value > 0).ToList()),
			ValidationErrors = eventCounts
				.Where(item => item.FormId == form.Id && item.EventType == FormAnalyticsEventType.ValidationError)
				.Sum(item => item.Count)
		};
	}

	private static int EventCount(
		IReadOnlyList<FormAnalyticsEventCount> counts,
		Guid formId,
		FormAnalyticsEventType eventType,
		Guid? fieldId) => counts
			.Where(item => item.FormId == formId && item.EventType == eventType && item.FieldId == fieldId)
			.Sum(item => item.Count);

	private static List<FormAnalyticsTrendPoint> BuildTrends(
		IReadOnlyList<FormAnalyticsSession> sessions,
		IReadOnlyList<ClubFormSubmission> submissions,
		bool weekly)
	{
		DateTime Bucket(DateTime value)
		{
			var date = value.Date;
			return weekly ? date.AddDays(-(((int)date.DayOfWeek + 6) % 7)) : date;
		}

		var dates = sessions.Select(item => Bucket(item.StartedAt))
			.Concat(submissions.Select(item => Bucket(item.SubmittedAt)))
			.Distinct()
			.Order()
			.ToList();

		return dates.Select(date =>
		{
			var views = sessions.Count(item => Bucket(item.StartedAt) == date);
			var starts = sessions.Count(item => item.HasInteracted && Bucket(item.StartedAt) == date);
			var submitted = submissions.Count(item => Bucket(item.SubmittedAt) == date);
			return new FormAnalyticsTrendPoint
			{
				Date = date,
				Views = views,
				Starts = starts,
				Submissions = submitted,
				CompletionRate = Percentage(submitted, starts)
			};
		}).ToList();
	}

	private static string GetVisitorKey(FormAnalyticsSession session) => session.UserId.HasValue
		? $"user:{session.UserId.Value:D}"
		: $"session:{session.Id:D}";

	private static double Percentage(int numerator, int denominator) => denominator <= 0
		? 0
		: Math.Round(Math.Min(100, numerator * 100d / denominator), 1);

	private static long Average(IReadOnlyCollection<long> values) => values.Count == 0
		? 0
		: (long)Math.Round(values.Average());

	private static long Median(IReadOnlyList<long> sortedValues)
	{
		if (sortedValues.Count == 0) return 0;
		var middle = sortedValues.Count / 2;
		return sortedValues.Count % 2 == 0
			? (sortedValues[middle - 1] + sortedValues[middle]) / 2
			: sortedValues[middle];
	}
}
