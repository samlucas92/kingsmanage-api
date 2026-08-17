using KingsManage;

namespace KingsManage.Tests.Unit.Forms;

public sealed class FormAnalyticsReportBuilderTests
{
	private static readonly Guid FormId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid FieldId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	[Test]
	public void OverviewCalculatesStartsCompletionAndAbandonmentFromSessions()
	{
		var form = BuildForm();
		var completedSession = BuildSession(hasInteracted: true, submitted: true, durationMs: 100_000);
		var abandonedSession = BuildSession(hasInteracted: true, submitted: false, durationMs: 40_000);
		var viewOnlySession = BuildSession(hasInteracted: false, submitted: false, durationMs: 5_000);
		var submissions = new[] { BuildSubmission(completedSession.Id) };

		var report = FormAnalyticsReportBuilder.BuildOverview(
			[form], [completedSession, abandonedSession, viewOnlySession], submissions, [], null, null);

		Assert.Multiple(() =>
		{
			Assert.That(report.TotalViews, Is.EqualTo(3));
			Assert.That(report.Starts, Is.EqualTo(2));
			Assert.That(report.Submissions, Is.EqualTo(1));
			Assert.That(report.CompletionRate, Is.EqualTo(50));
			Assert.That(report.Forms.Single().Abandoned, Is.EqualTo(1));
			Assert.That(report.Forms.Single().AbandonmentRate, Is.EqualTo(50));
		});
	}

	[Test]
	public void ViewOnlySessionDoesNotCountAsAbandoned()
	{
		var report = FormAnalyticsReportBuilder.BuildOverview(
			[BuildForm()], [BuildSession(false, false, 0)], [], [], null, null);

		Assert.That(report.Forms.Single().Abandoned, Is.Zero);
	}

	[Test]
	public void UniqueVisitorsDeduplicateAuthenticatedUsersButKeepAnonymousSessionsSeparate()
	{
		var userId = Guid.NewGuid();
		var sessions = new[]
		{
			BuildSession(false, false, 0, userId),
			BuildSession(false, false, 0, userId),
			BuildSession(false, false, 0),
			BuildSession(false, false, 0)
		};

		var report = FormAnalyticsReportBuilder.BuildOverview([BuildForm()], sessions, [], [], null, null);

		Assert.That(report.UniqueVisitors, Is.EqualTo(3));
	}

	[Test]
	public void DetailReportsEngagedTimingAndPrivacySafeFieldCounts()
	{
		var completed = BuildSession(true, true, 120_000);
		var abandoned = BuildSession(true, false, 30_000);
		var eventCounts = new[]
		{
			BuildEventCount(FormAnalyticsEventType.FieldInteracted, FieldId, 1),
			BuildEventCount(FormAnalyticsEventType.ValidationError, FieldId, 2)
		};

		var report = FormAnalyticsReportBuilder.BuildDetail(
			BuildForm(), [completed, abandoned], [BuildSubmission(completed.Id)], eventCounts, null, null);

		Assert.Multiple(() =>
		{
			Assert.That(report.AverageEngagedDurationMs, Is.EqualTo(75_000));
			Assert.That(report.MedianEngagedDurationMs, Is.EqualTo(75_000));
			Assert.That(report.AverageCompletedDurationMs, Is.EqualTo(120_000));
			Assert.That(report.AverageAbandonedDurationMs, Is.EqualTo(30_000));
			Assert.That(report.Fields.Single().Interactions, Is.EqualTo(1));
			Assert.That(report.Fields.Single().ValidationErrors, Is.EqualTo(2));
		});
	}

	[Test]
	public void EmptyAnalyticsReturnsZeroMetricsAndNoAttentionNoise()
	{
		var report = FormAnalyticsReportBuilder.BuildOverview([BuildForm()], [], [], [], null, null);

		Assert.Multiple(() =>
		{
			Assert.That(report.CompletionRate, Is.Zero);
			Assert.That(report.ViewConversionRate, Is.Zero);
			Assert.That(report.NeedsAttention, Is.Empty);
		});
	}

	private static ClubForm BuildForm() => new()
	{
		Id = FormId,
		Title = "Player registration",
		Questions = [new ClubFormQuestion { Id = FieldId, Prompt = "Player name", IsRequired = true }]
	};

	private static FormAnalyticsSession BuildSession(bool hasInteracted, bool submitted, long durationMs, Guid? userId = null) => new()
	{
		Id = Guid.NewGuid(), FormId = FormId, UserId = userId, StartedAt = DateTime.UtcNow.Date,
		HasInteracted = hasInteracted, SubmittedAt = submitted ? DateTime.UtcNow : null, EngagedDurationMs = durationMs
	};

	private static ClubFormSubmission BuildSubmission(Guid sessionId) => new()
	{
		Id = Guid.NewGuid(), FormId = FormId, AnalyticsSessionId = sessionId, SubmittedAt = DateTime.UtcNow
	};

	private static FormAnalyticsEventCount BuildEventCount(FormAnalyticsEventType type, Guid fieldId, int count) => new()
	{
		FormId = FormId, EventType = type, FieldId = fieldId, Count = count
	};
}
