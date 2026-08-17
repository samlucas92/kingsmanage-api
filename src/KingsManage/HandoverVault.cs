namespace KingsManage;

public enum OperationalTaskStatus
{
	NotStarted,
	InProgress,
	Blocked,
	Completed,
	Cancelled
}

public enum OperationalTaskRecurrence
{
	None,
	Weekly,
	Monthly,
	Yearly,
	CustomInterval
}

public enum HandoverStatus
{
	Draft,
	InProgress,
	ReadyForReview,
	Completed,
	Cancelled
}

public enum HandoverItemType
{
	Responsibility,
	OutstandingTask,
	OrganizationDocument,
	Contact,
	AccessTransfer,
	General
}

public enum HandoverItemStatus
{
	Pending,
	Confirmed,
	NotApplicable,
	Blocked
}

public enum ContinuityWarningSeverity
{
	Critical,
	Attention,
	Complete
}

public sealed class OperationalRole
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public int DisplayOrder { get; set; }
	public Guid? PrimaryOwnerUserId { get; set; }
	public List<Guid> SupportingOwnerUserIds { get; set; } = [];
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public Guid CreatedByUserId { get; set; }
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	public Guid UpdatedByUserId { get; set; }
}

public sealed class RoleResponsibility
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public Guid OperationalRoleId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Summary { get; set; } = string.Empty;
	public string Category { get; set; } = "Other";
	public string Frequency { get; set; } = string.Empty;
	public string TypicalDueDateDescription { get; set; } = string.Empty;
	public bool IsCritical { get; set; }
	public bool IsActive { get; set; } = true;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public Guid CreatedByUserId { get; set; }
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	public Guid UpdatedByUserId { get; set; }
}

public sealed class HandoverDocumentLink
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public Guid? OperationalRoleId { get; set; }
	public Guid? ResponsibilityId { get; set; }
	public Guid OrganizationDocumentId { get; set; }
	public string Purpose { get; set; } = string.Empty;
	public bool IsRequiredForHandover { get; set; }
	public int DisplayOrder { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public Guid CreatedByUserId { get; set; }
}

public sealed class OperationalTask
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public Guid OperationalRoleId { get; set; }
	public Guid? ResponsibilityId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public DateTime? DueAt { get; set; }
	public List<Guid> AssignedUserIds { get; set; } = [];
	public OperationalTaskStatus Status { get; set; }
	public DateTime? CompletedAt { get; set; }
	public Guid? CompletedByUserId { get; set; }
	public string CompletionNotes { get; set; } = string.Empty;
	public OperationalTaskRecurrence Recurrence { get; set; }
	public int RecurrenceInterval { get; set; } = 1;
	public int? CustomIntervalDays { get; set; }
	public DateTime? NextOccurrenceAt { get; set; }
	public Guid? RecurrenceSourceTaskId { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public Guid CreatedByUserId { get; set; }
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	public Guid UpdatedByUserId { get; set; }
}

public sealed class OperationalContact
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public Guid? OperationalRoleId { get; set; }
	public Guid? ResponsibilityId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string OrganizationName { get; set; } = string.Empty;
	public string Purpose { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string Phone { get; set; } = string.Empty;
	public string Notes { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public Guid CreatedByUserId { get; set; }
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	public Guid UpdatedByUserId { get; set; }
}

public sealed class HandoverRecord
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public Guid OperationalRoleId { get; set; }
	public Guid? OutgoingUserId { get; set; }
	public Guid? IncomingUserId { get; set; }
	public HandoverStatus Status { get; set; } = HandoverStatus.Draft;
	public DateTime StartedAt { get; set; } = DateTime.UtcNow;
	public DateTime? DueAt { get; set; }
	public DateTime? ReadyForReviewAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public Guid CreatedByUserId { get; set; }
	public Guid? CompletedByUserId { get; set; }
	public string Notes { get; set; } = string.Empty;
	public List<HandoverItem> Items { get; set; } = [];
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class HandoverItem
{
	public Guid Id { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public HandoverItemType ItemType { get; set; }
	public Guid? SourceEntityId { get; set; }
	public HandoverItemStatus Status { get; set; }
	public bool RequiresOutgoingConfirmation { get; set; }
	public bool RequiresIncomingConfirmation { get; set; }
	public DateTime? OutgoingConfirmedAt { get; set; }
	public Guid? OutgoingConfirmedByUserId { get; set; }
	public DateTime? IncomingConfirmedAt { get; set; }
	public Guid? IncomingConfirmedByUserId { get; set; }
	public string Notes { get; set; } = string.Empty;
	public int DisplayOrder { get; set; }
	public string? DocumentTitleAtCompletion { get; set; }
	public string? DocumentPurposeAtCompletion { get; set; }
	public DateTime? AcknowledgedAt { get; set; }
}

public sealed class HandoverAuditEntry
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public string EntityType { get; set; } = string.Empty;
	public Guid EntityId { get; set; }
	public string Action { get; set; } = string.Empty;
	public string Detail { get; set; } = string.Empty;
	public Guid UserId { get; set; }
	public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

public sealed class ContinuityWarning
{
	public string Code { get; set; } = string.Empty;
	public ContinuityWarningSeverity Severity { get; set; }
	public string Message { get; set; } = string.Empty;
	public string EntityType { get; set; } = string.Empty;
	public Guid EntityId { get; set; }
	public string ActionPath { get; set; } = string.Empty;
}

public sealed class HandoverVaultSnapshot
{
	public List<OperationalRole> Roles { get; set; } = [];
	public List<RoleResponsibility> Responsibilities { get; set; } = [];
	public List<HandoverDocumentLink> DocumentLinks { get; set; } = [];
	public List<OperationalTask> Tasks { get; set; } = [];
	public List<OperationalContact> Contacts { get; set; } = [];
	public List<HandoverRecord> Handovers { get; set; } = [];
	public List<ContinuityWarning> Warnings { get; set; } = [];
}

public interface IHandoverVaultService
{
	Task<HandoverVaultSnapshot> GetSnapshotAsync(Guid? userId, bool isAdmin, CancellationToken cancellationToken = default);
	Task<OperationalRole?> GetRoleAsync(Guid id, CancellationToken cancellationToken = default);
	Task<OperationalRole> SaveRoleAsync(OperationalRole role, Guid userId, CancellationToken cancellationToken = default);
	Task<RoleResponsibility> SaveResponsibilityAsync(RoleResponsibility responsibility, Guid userId, CancellationToken cancellationToken = default);
	Task<HandoverDocumentLink> LinkDocumentAsync(HandoverDocumentLink link, Guid userId, CancellationToken cancellationToken = default);
	Task<bool> UnlinkDocumentAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
	Task<OperationalTask> SaveTaskAsync(OperationalTask task, Guid userId, CancellationToken cancellationToken = default);
	Task<OperationalContact> SaveContactAsync(OperationalContact contact, Guid userId, CancellationToken cancellationToken = default);
	Task<HandoverRecord> CreateHandoverAsync(HandoverRecord handover, IReadOnlyList<string> accessTransfers, IReadOnlyList<string> additionalItems, Guid userId, CancellationToken cancellationToken = default);
	Task<HandoverRecord?> GetHandoverAsync(Guid id, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
	Task<HandoverRecord?> ConfirmItemAsync(Guid handoverId, Guid itemId, Guid userId, bool confirmed, string notes, CancellationToken cancellationToken = default);
	Task<HandoverRecord?> SetItemStatusAsync(Guid handoverId, Guid itemId, HandoverItemStatus status, Guid userId, string notes, CancellationToken cancellationToken = default);
	Task<HandoverRecord?> SetHandoverStatusAsync(Guid handoverId, HandoverStatus status, Guid userId, CancellationToken cancellationToken = default);
	Task<HandoverRecord?> RefreshHandoverAsync(Guid handoverId, Guid userId, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<HandoverAuditEntry>> GetAuditAsync(Guid entityId, CancellationToken cancellationToken = default);
	Task NotifyDocumentArchivedAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);
}

public interface IOrganizationDocumentService
{
	Task<IReadOnlyList<ClubPost>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<ClubPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<ClubPost> CreateAsync(ClubPost document, CancellationToken cancellationToken = default);
	Task<ClubPost?> UpdateAsync(ClubPost document, CancellationToken cancellationToken = default);
	Task<ClubPost?> SetArchivedAsync(Guid id, bool archived, CancellationToken cancellationToken = default);
}

public static class OperationalTaskRecurrenceCalculator
{
	public static DateTime? CalculateNext(OperationalTask task)
	{
		if (!task.DueAt.HasValue || task.Recurrence == OperationalTaskRecurrence.None)
		{
			return null;
		}

		var interval = Math.Max(1, task.RecurrenceInterval);
		return task.Recurrence switch
		{
			OperationalTaskRecurrence.Weekly => task.DueAt.Value.AddDays(7 * interval),
			OperationalTaskRecurrence.Monthly => task.DueAt.Value.AddMonths(interval),
			OperationalTaskRecurrence.Yearly => task.DueAt.Value.AddYears(interval),
			OperationalTaskRecurrence.CustomInterval => task.DueAt.Value.AddDays(Math.Max(1, task.CustomIntervalDays ?? interval)),
			_ => null
		};
	}
}
