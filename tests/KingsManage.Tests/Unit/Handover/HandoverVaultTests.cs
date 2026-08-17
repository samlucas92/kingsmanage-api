using KingsManage;
using KingsManage.Mongo.Services;
using NUnit.Framework;

namespace KingsManage.Tests.Unit.Handover;

[TestFixture]
public sealed class HandoverVaultTests
{
	[TestCase(OperationalTaskRecurrence.Weekly, 1, 7)]
	[TestCase(OperationalTaskRecurrence.Weekly, 2, 14)]
	[TestCase(OperationalTaskRecurrence.CustomInterval, 1, 10)]
	public void CalculateNext_UsesDeterministicInterval(
		OperationalTaskRecurrence recurrence,
		int interval,
		int expectedDays)
	{
		var dueAt = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
		var task = new OperationalTask
		{
			DueAt = dueAt,
			Recurrence = recurrence,
			RecurrenceInterval = interval,
			CustomIntervalDays = recurrence == OperationalTaskRecurrence.CustomInterval ? expectedDays : null
		};

		Assert.That(OperationalTaskRecurrenceCalculator.CalculateNext(task), Is.EqualTo(dueAt.AddDays(expectedDays)));
	}

	[Test]
	public void CalculateNext_Monthly_PreservesCalendarSemantics()
	{
		var task = new OperationalTask
		{
			DueAt = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc),
			Recurrence = OperationalTaskRecurrence.Monthly,
			RecurrenceInterval = 1
		};

		Assert.That(OperationalTaskRecurrenceCalculator.CalculateNext(task), Is.EqualTo(new DateTime(2026, 2, 28, 12, 0, 0, DateTimeKind.Utc)));
	}

	[Test]
	public void BuildWarnings_ExplainsEachDeterministicCondition()
	{
		var roleId = Guid.NewGuid();
		var responsibilityId = Guid.NewGuid();
		var taskId = Guid.NewGuid();
		var warnings = HandoverVaultService.BuildWarnings(
			[new OperationalRole { Id = roleId, Name = "Secretary", IsActive = true }],
			[new RoleResponsibility { Id = responsibilityId, OperationalRoleId = roleId, Title = "Annual return", IsActive = true, IsCritical = true }],
			[],
			[new OperationalTask { Id = taskId, OperationalRoleId = roleId, Title = "Submit return", DueAt = DateTime.UtcNow.AddDays(-1), Status = OperationalTaskStatus.InProgress }],
			[],
			[],
			[]);

		Assert.Multiple(() =>
		{
			Assert.That(warnings, Has.Some.Matches<ContinuityWarning>(warning => warning.Code == "role-no-owner" && warning.EntityId == roleId && warning.Severity == ContinuityWarningSeverity.Critical));
			Assert.That(warnings, Has.Some.Matches<ContinuityWarning>(warning => warning.Code == "critical-no-document" && warning.EntityId == responsibilityId));
			Assert.That(warnings, Has.Some.Matches<ContinuityWarning>(warning => warning.Code == "task-overdue" && warning.EntityId == taskId));
			Assert.That(warnings.All(warning => !string.IsNullOrWhiteSpace(warning.Message) && !string.IsNullOrWhiteSpace(warning.ActionPath)), Is.True);
		});
	}

	[Test]
	public void BuildWarnings_ClearsResolvedConditions()
	{
		var roleId = Guid.NewGuid();
		var responsibilityId = Guid.NewGuid();
		var documentId = Guid.NewGuid();
		var warnings = HandoverVaultService.BuildWarnings(
			[new OperationalRole { Id = roleId, Name = "Treasurer", IsActive = true, PrimaryOwnerUserId = Guid.NewGuid(), SupportingOwnerUserIds = [Guid.NewGuid()] }],
			[new RoleResponsibility { Id = responsibilityId, OperationalRoleId = roleId, Title = "Accounts", IsActive = true, IsCritical = true }],
			[new HandoverDocumentLink { ResponsibilityId = responsibilityId, OrganizationDocumentId = documentId }],
			[],
			[],
			[],
			[new ClubPost { Id = documentId, Type = ClubPostType.OrganizationDocument, Title = "Accounts process" }]);

		Assert.That(warnings, Is.Empty);
	}
}
