using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class HandoverVaultService : IHandoverVaultService
{
	private readonly IMongoCollection<OperationalRole> roles;
	private readonly IMongoCollection<RoleResponsibility> responsibilities;
	private readonly IMongoCollection<HandoverDocumentLink> documentLinks;
	private readonly IMongoCollection<OperationalTask> tasks;
	private readonly IMongoCollection<OperationalContact> contacts;
	private readonly IMongoCollection<HandoverRecord> handovers;
	private readonly IMongoCollection<HandoverAuditEntry> audit;
	private readonly ITenantContext tenant;
	private readonly IUserService users;
	private readonly IOrganizationDocumentService documents;
	private readonly IClubNotificationService notifications;

	public HandoverVaultService(
		MongoContext context,
		ITenantContext tenant,
		IUserService users,
		IOrganizationDocumentService documents,
		IClubNotificationService notifications)
	{
		roles = context.Database.GetCollection<OperationalRole>("operationalRoles");
		responsibilities = context.Database.GetCollection<RoleResponsibility>("roleResponsibilities");
		documentLinks = context.Database.GetCollection<HandoverDocumentLink>("handoverDocumentLinks");
		tasks = context.Database.GetCollection<OperationalTask>("operationalTasks");
		contacts = context.Database.GetCollection<OperationalContact>("operationalContacts");
		handovers = context.Database.GetCollection<HandoverRecord>("handoverRecords");
		audit = context.Database.GetCollection<HandoverAuditEntry>("handoverAudit");
		this.tenant = tenant;
		this.users = users;
		this.documents = documents;
		this.notifications = notifications;
	}

	public async Task<HandoverVaultSnapshot> GetSnapshotAsync(
		Guid? userId,
		bool isAdmin,
		CancellationToken cancellationToken = default)
	{
		var allRoles = await roles.Find(OrganizationFilter<OperationalRole>()).SortBy(role => role.DisplayOrder).ThenBy(role => role.Name).ToListAsync(cancellationToken);
		if (!isAdmin && userId.HasValue)
		{
			allRoles = allRoles.Where(role => role.PrimaryOwnerUserId == userId || role.SupportingOwnerUserIds.Contains(userId.Value)).ToList();
		}

		var roleIds = allRoles.Select(role => role.Id).ToHashSet();
		var allResponsibilities = await responsibilities.Find(OrganizationFilter<RoleResponsibility>()).ToListAsync(cancellationToken);
		var allLinks = await documentLinks.Find(OrganizationFilter<HandoverDocumentLink>()).ToListAsync(cancellationToken);
		var allTasks = await tasks.Find(OrganizationFilter<OperationalTask>()).SortBy(task => task.DueAt).ToListAsync(cancellationToken);
		if (userId.HasValue)
		{
			await NotifyTaskDeadlinesAsync(allTasks, userId.Value, cancellationToken);
		}
		var allContacts = isAdmin
			? await contacts.Find(OrganizationFilter<OperationalContact>()).ToListAsync(cancellationToken)
			: [];
		var allHandovers = await handovers.Find(OrganizationFilter<HandoverRecord>()).SortByDescending(item => item.StartedAt).ToListAsync(cancellationToken);

		if (!isAdmin)
		{
			allResponsibilities = allResponsibilities.Where(item => roleIds.Contains(item.OperationalRoleId)).ToList();
			var responsibilityIds = allResponsibilities.Select(item => item.Id).ToHashSet();
			allLinks = allLinks.Where(link =>
				(link.OperationalRoleId.HasValue && roleIds.Contains(link.OperationalRoleId.Value)) ||
				(link.ResponsibilityId.HasValue && responsibilityIds.Contains(link.ResponsibilityId.Value))).ToList();
			allTasks = allTasks.Where(task => roleIds.Contains(task.OperationalRoleId) && task.AssignedUserIds.Contains(userId!.Value)).ToList();
			allHandovers = allHandovers.Where(item => item.OutgoingUserId == userId || item.IncomingUserId == userId).ToList();
		}

		var organizationDocuments = await documents.GetAllAsync(cancellationToken);
		return new HandoverVaultSnapshot
		{
			Roles = allRoles,
			Responsibilities = allResponsibilities,
			DocumentLinks = allLinks,
			Tasks = allTasks,
			Contacts = allContacts,
			Handovers = allHandovers,
			Warnings = BuildWarnings(allRoles, allResponsibilities, allLinks, allTasks, allContacts, allHandovers, organizationDocuments)
		};
	}

	public async Task<OperationalRole?> GetRoleAsync(Guid id, CancellationToken cancellationToken = default) =>
		await roles.Find(OrganizationFilter<OperationalRole>() & Builders<OperationalRole>.Filter.Eq(role => role.Id, id)).FirstOrDefaultAsync(cancellationToken);

	public async Task<OperationalRole> SaveRoleAsync(OperationalRole role, Guid userId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(role.Name)) throw new ArgumentException("Role name is required.");
		await ValidateUsersAsync(role.SupportingOwnerUserIds.Append(role.PrimaryOwnerUserId ?? Guid.Empty), cancellationToken);
		role.SupportingOwnerUserIds = role.SupportingOwnerUserIds.Where(id => id != Guid.Empty && id != role.PrimaryOwnerUserId).Distinct().ToList();
		var now = DateTime.UtcNow;
		var existing = role.Id == Guid.Empty ? null : await GetRoleAsync(role.Id, cancellationToken);
		role.Id = existing?.Id ?? Guid.NewGuid();
		role.OrganizationId = tenant.OrganizationId;
		role.Name = role.Name.Trim();
		role.Description = role.Description.Trim();
		role.CreatedAt = existing?.CreatedAt ?? now;
		role.CreatedByUserId = existing?.CreatedByUserId ?? userId;
		role.UpdatedAt = now;
		role.UpdatedByUserId = userId;
		await roles.ReplaceOneAsync(
			OrganizationFilter<OperationalRole>() & Builders<OperationalRole>.Filter.Eq(item => item.Id, role.Id),
			role,
			new ReplaceOptions { IsUpsert = existing is null },
			cancellationToken);
		await RecordAuditAsync("OperationalRole", role.Id, existing is null ? "Created" : "Updated", role.Name, userId, cancellationToken);
		if (role.PrimaryOwnerUserId.HasValue && role.PrimaryOwnerUserId != existing?.PrimaryOwnerUserId)
		{
			await NotifyAsync(NotificationType.OperationalRoleAssigned, NotificationSourceType.OperationalRole, role.Id, "Operational role assigned", $"You are now the primary owner of {role.Name}.", $"/handover/roles/{role.Id}", [role.PrimaryOwnerUserId.Value], userId, cancellationToken);
		}
		return role;
	}

	public async Task<RoleResponsibility> SaveResponsibilityAsync(RoleResponsibility responsibility, Guid userId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(responsibility.Title)) throw new ArgumentException("Responsibility title is required.");
		await RequireRoleAsync(responsibility.OperationalRoleId, cancellationToken);
		var existing = responsibility.Id == Guid.Empty ? null : await responsibilities.Find(OrganizationFilter<RoleResponsibility>() & Builders<RoleResponsibility>.Filter.Eq(item => item.Id, responsibility.Id)).FirstOrDefaultAsync(cancellationToken);
		var now = DateTime.UtcNow;
		responsibility.Id = existing?.Id ?? Guid.NewGuid();
		responsibility.OrganizationId = tenant.OrganizationId;
		responsibility.Title = responsibility.Title.Trim();
		responsibility.Summary = responsibility.Summary.Trim();
		responsibility.Category = string.IsNullOrWhiteSpace(responsibility.Category) ? "Other" : responsibility.Category.Trim();
		responsibility.CreatedAt = existing?.CreatedAt ?? now;
		responsibility.CreatedByUserId = existing?.CreatedByUserId ?? userId;
		responsibility.UpdatedAt = now;
		responsibility.UpdatedByUserId = userId;
		await responsibilities.ReplaceOneAsync(OrganizationFilter<RoleResponsibility>() & Builders<RoleResponsibility>.Filter.Eq(item => item.Id, responsibility.Id), responsibility, new ReplaceOptions { IsUpsert = existing is null }, cancellationToken);
		await RecordAuditAsync("Responsibility", responsibility.Id, existing is null ? "Created" : "Updated", responsibility.Title, userId, cancellationToken);
		return responsibility;
	}

	public async Task<HandoverDocumentLink> LinkDocumentAsync(HandoverDocumentLink link, Guid userId, CancellationToken cancellationToken = default)
	{
		if (link.OperationalRoleId.HasValue == link.ResponsibilityId.HasValue) throw new ArgumentException("Link exactly one role or responsibility.");
		if (link.OperationalRoleId.HasValue) await RequireRoleAsync(link.OperationalRoleId.Value, cancellationToken);
		if (link.ResponsibilityId.HasValue) await RequireResponsibilityAsync(link.ResponsibilityId.Value, cancellationToken);
		if (await documents.GetByIdAsync(link.OrganizationDocumentId, cancellationToken) is null) throw new ArgumentException("Organization document is unavailable.");
		link.Id = link.Id == Guid.Empty ? Guid.NewGuid() : link.Id;
		link.OrganizationId = tenant.OrganizationId;
		link.Purpose = link.Purpose.Trim();
		link.CreatedAt = DateTime.UtcNow;
		link.CreatedByUserId = userId;
		await documentLinks.ReplaceOneAsync(OrganizationFilter<HandoverDocumentLink>() & Builders<HandoverDocumentLink>.Filter.Eq(item => item.Id, link.Id), link, new ReplaceOptions { IsUpsert = true }, cancellationToken);
		await RecordAuditAsync("DocumentLink", link.Id, "Linked", link.Purpose, userId, cancellationToken);
		return link;
	}

	public async Task<bool> UnlinkDocumentAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
	{
		var result = await documentLinks.DeleteOneAsync(OrganizationFilter<HandoverDocumentLink>() & Builders<HandoverDocumentLink>.Filter.Eq(item => item.Id, id), cancellationToken);
		if (result.DeletedCount > 0) await RecordAuditAsync("DocumentLink", id, "Unlinked", "Organization document relationship removed; the document was retained.", userId, cancellationToken);
		return result.DeletedCount > 0;
	}

	public async Task<OperationalTask> SaveTaskAsync(OperationalTask task, Guid userId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(task.Title)) throw new ArgumentException("Task title is required.");
		await RequireRoleAsync(task.OperationalRoleId, cancellationToken);
		if (task.ResponsibilityId.HasValue) await RequireResponsibilityAsync(task.ResponsibilityId.Value, cancellationToken);
		await ValidateUsersAsync(task.AssignedUserIds, cancellationToken);
		var existing = task.Id == Guid.Empty ? null : await tasks.Find(OrganizationFilter<OperationalTask>() & Builders<OperationalTask>.Filter.Eq(item => item.Id, task.Id)).FirstOrDefaultAsync(cancellationToken);
		var now = DateTime.UtcNow;
		task.Id = existing?.Id ?? Guid.NewGuid();
		task.OrganizationId = tenant.OrganizationId;
		task.Title = task.Title.Trim();
		task.Description = task.Description.Trim();
		task.AssignedUserIds = task.AssignedUserIds.Where(id => id != Guid.Empty).Distinct().ToList();
		task.CreatedAt = existing?.CreatedAt ?? now;
		task.CreatedByUserId = existing?.CreatedByUserId ?? userId;
		task.UpdatedAt = now;
		task.UpdatedByUserId = userId;
		if (task.Status == OperationalTaskStatus.Completed)
		{
			task.CompletedAt ??= now;
			task.CompletedByUserId ??= userId;
			task.NextOccurrenceAt = OperationalTaskRecurrenceCalculator.CalculateNext(task);
		}
		else
		{
			task.CompletedAt = null;
			task.CompletedByUserId = null;
		}
		await tasks.ReplaceOneAsync(OrganizationFilter<OperationalTask>() & Builders<OperationalTask>.Filter.Eq(item => item.Id, task.Id), task, new ReplaceOptions { IsUpsert = existing is null }, cancellationToken);
		if (task.Status == OperationalTaskStatus.Completed && existing?.Status != OperationalTaskStatus.Completed && task.NextOccurrenceAt.HasValue)
		{
			await CreateNextOccurrenceAsync(task, userId, cancellationToken);
		}
		await RecordAuditAsync("OperationalTask", task.Id, existing is null ? "Created" : task.Status == OperationalTaskStatus.Completed ? "Completed" : "Updated", task.Title, userId, cancellationToken);
		var addedAssignees = task.AssignedUserIds.Except(existing?.AssignedUserIds ?? []).ToList();
		if (addedAssignees.Count > 0) await NotifyAsync(NotificationType.OperationalTaskAssigned, NotificationSourceType.OperationalTask, task.Id, "Operational task assigned", task.Title, "/handover/tasks", addedAssignees, userId, cancellationToken);
		return task;
	}

	public async Task<OperationalContact> SaveContactAsync(OperationalContact contact, Guid userId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(contact.Name)) throw new ArgumentException("Contact name is required.");
		if (contact.OperationalRoleId.HasValue) await RequireRoleAsync(contact.OperationalRoleId.Value, cancellationToken);
		if (contact.ResponsibilityId.HasValue) await RequireResponsibilityAsync(contact.ResponsibilityId.Value, cancellationToken);
		var existing = contact.Id == Guid.Empty ? null : await contacts.Find(OrganizationFilter<OperationalContact>() & Builders<OperationalContact>.Filter.Eq(item => item.Id, contact.Id)).FirstOrDefaultAsync(cancellationToken);
		var now = DateTime.UtcNow;
		contact.Id = existing?.Id ?? Guid.NewGuid();
		contact.OrganizationId = tenant.OrganizationId;
		contact.Name = contact.Name.Trim();
		contact.OrganizationName = contact.OrganizationName.Trim();
		contact.Purpose = contact.Purpose.Trim();
		contact.CreatedAt = existing?.CreatedAt ?? now;
		contact.CreatedByUserId = existing?.CreatedByUserId ?? userId;
		contact.UpdatedAt = now;
		contact.UpdatedByUserId = userId;
		await contacts.ReplaceOneAsync(OrganizationFilter<OperationalContact>() & Builders<OperationalContact>.Filter.Eq(item => item.Id, contact.Id), contact, new ReplaceOptions { IsUpsert = existing is null }, cancellationToken);
		await RecordAuditAsync("OperationalContact", contact.Id, existing is null ? "Created" : "Updated", contact.Name, userId, cancellationToken);
		return contact;
	}

	public async Task<HandoverRecord> CreateHandoverAsync(HandoverRecord handover, IReadOnlyList<string> accessTransfers, IReadOnlyList<string> additionalItems, Guid userId, CancellationToken cancellationToken = default)
	{
		await RequireRoleAsync(handover.OperationalRoleId, cancellationToken);
		await ValidateUsersAsync([handover.OutgoingUserId ?? Guid.Empty, handover.IncomingUserId ?? Guid.Empty], cancellationToken);
		handover.Id = Guid.NewGuid();
		handover.OrganizationId = tenant.OrganizationId;
		handover.Status = HandoverStatus.InProgress;
		handover.StartedAt = DateTime.UtcNow;
		handover.CreatedAt = handover.StartedAt;
		handover.UpdatedAt = handover.StartedAt;
		handover.CreatedByUserId = userId;
		handover.Items = await GenerateItemsAsync(handover.OperationalRoleId, handover.OutgoingUserId.HasValue, handover.IncomingUserId.HasValue, cancellationToken);
		foreach (var title in accessTransfers.Where(title => !string.IsNullOrWhiteSpace(title)))
		{
			handover.Items.Add(new HandoverItem
			{
				Id = Guid.NewGuid(),
				Title = title.Trim(),
				Description = "Describe the account and transfer action only. Never record a password, recovery code, API key or other secret.",
				ItemType = HandoverItemType.AccessTransfer,
				RequiresOutgoingConfirmation = handover.OutgoingUserId.HasValue,
				RequiresIncomingConfirmation = handover.IncomingUserId.HasValue,
				DisplayOrder = handover.Items.Count
			});
		}
		foreach (var title in additionalItems.Where(title => !string.IsNullOrWhiteSpace(title)))
		{
			handover.Items.Add(new HandoverItem
			{
				Id = Guid.NewGuid(),
				Title = title.Trim(),
				ItemType = HandoverItemType.General,
				RequiresOutgoingConfirmation = handover.OutgoingUserId.HasValue,
				RequiresIncomingConfirmation = handover.IncomingUserId.HasValue,
				DisplayOrder = handover.Items.Count
			});
		}
		await handovers.InsertOneAsync(handover, cancellationToken: cancellationToken);
		await RecordAuditAsync("Handover", handover.Id, "Started", $"Generated {handover.Items.Count} checklist items.", userId, cancellationToken);
		var recipients = new[] { handover.OutgoingUserId, handover.IncomingUserId }.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
		if (recipients.Count > 0) await NotifyAsync(NotificationType.HandoverStarted, NotificationSourceType.Handover, handover.Id, "Handover started", "A role handover needs your review.", $"/handover/records/{handover.Id}", recipients, userId, cancellationToken);
		return handover;
	}

	public async Task<HandoverRecord?> GetHandoverAsync(Guid id, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
	{
		var handover = await handovers.Find(OrganizationFilter<HandoverRecord>() & Builders<HandoverRecord>.Filter.Eq(item => item.Id, id)).FirstOrDefaultAsync(cancellationToken);
		return handover is not null && (isAdmin || handover.OutgoingUserId == userId || handover.IncomingUserId == userId) ? handover : null;
	}

	public async Task<HandoverRecord?> ConfirmItemAsync(Guid handoverId, Guid itemId, Guid userId, bool confirmed, string notes, CancellationToken cancellationToken = default)
	{
		var handover = await GetHandoverAsync(handoverId, userId, false, cancellationToken);
		if (handover is null || handover.Status is HandoverStatus.Completed or HandoverStatus.Cancelled) return null;
		var item = handover.Items.FirstOrDefault(item => item.Id == itemId);
		if (item is null) return null;
		var now = DateTime.UtcNow;
		var canConfirm = false;
		if (handover.OutgoingUserId == userId && item.RequiresOutgoingConfirmation)
		{
			item.OutgoingConfirmedAt = confirmed ? now : null;
			item.OutgoingConfirmedByUserId = confirmed ? userId : null;
			canConfirm = true;
		}
		if (handover.IncomingUserId == userId && item.RequiresIncomingConfirmation)
		{
			item.IncomingConfirmedAt = confirmed ? now : null;
			item.IncomingConfirmedByUserId = confirmed ? userId : null;
			canConfirm = true;
		}
		if (!canConfirm) throw new UnauthorizedAccessException("Only the outgoing or incoming owner can confirm their own checklist state.");
		item.Notes = notes.Trim();
		item.Status = IsFullyConfirmed(item) ? HandoverItemStatus.Confirmed : HandoverItemStatus.Pending;
		handover.UpdatedAt = now;
		await ReplaceHandoverAsync(handover, cancellationToken);
		await RecordAuditAsync("HandoverItem", item.Id, confirmed ? "Confirmed" : "Reopened", item.Title, userId, cancellationToken);
		return handover;
	}

	public async Task<HandoverRecord?> SetItemStatusAsync(Guid handoverId, Guid itemId, HandoverItemStatus status, Guid userId, string notes, CancellationToken cancellationToken = default)
	{
		var handover = await handovers.Find(OrganizationFilter<HandoverRecord>() & Builders<HandoverRecord>.Filter.Eq(item => item.Id, handoverId)).FirstOrDefaultAsync(cancellationToken);
		if (handover is null || handover.Status is HandoverStatus.Completed or HandoverStatus.Cancelled) return null;
		var item = handover.Items.FirstOrDefault(item => item.Id == itemId);
		if (item is null) return null;
		item.Status = status;
		item.Notes = notes.Trim();
		if (status == HandoverItemStatus.Pending)
		{
			item.OutgoingConfirmedAt = null;
			item.OutgoingConfirmedByUserId = null;
			item.IncomingConfirmedAt = null;
			item.IncomingConfirmedByUserId = null;
		}
		handover.UpdatedAt = DateTime.UtcNow;
		await ReplaceHandoverAsync(handover, cancellationToken);
		await RecordAuditAsync("HandoverItem", item.Id, status.ToString(), item.Title, userId, cancellationToken);
		return handover;
	}

	public async Task<HandoverRecord?> SetHandoverStatusAsync(Guid handoverId, HandoverStatus status, Guid userId, CancellationToken cancellationToken = default)
	{
		var handover = await handovers.Find(OrganizationFilter<HandoverRecord>() & Builders<HandoverRecord>.Filter.Eq(item => item.Id, handoverId)).FirstOrDefaultAsync(cancellationToken);
		if (handover is null) return null;
		if (handover.Status == status) return handover;
		if (status == HandoverStatus.Completed && handover.Items.Any(item => item.Status is HandoverItemStatus.Pending or HandoverItemStatus.Blocked)) throw new InvalidOperationException("All required checklist items must be resolved before completion.");
		handover.Status = status;
		handover.UpdatedAt = DateTime.UtcNow;
		if (status == HandoverStatus.ReadyForReview) handover.ReadyForReviewAt = handover.UpdatedAt;
		if (status == HandoverStatus.Completed)
		{
			handover.CompletedAt = handover.UpdatedAt;
			handover.CompletedByUserId = userId;
			await SnapshotDocumentAcknowledgementsAsync(handover, cancellationToken);
		}
		await ReplaceHandoverAsync(handover, cancellationToken);
		await RecordAuditAsync("Handover", handover.Id, status.ToString(), handover.Notes, userId, cancellationToken);
		var recipients = new[] { handover.OutgoingUserId, handover.IncomingUserId }.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
		if (recipients.Count > 0 && status is HandoverStatus.ReadyForReview or HandoverStatus.Completed)
		{
			await NotifyAsync(status == HandoverStatus.Completed ? NotificationType.HandoverCompleted : NotificationType.HandoverReadyForReview, NotificationSourceType.Handover, handover.Id, status == HandoverStatus.Completed ? "Handover completed" : "Handover ready for review", "The role handover status has changed.", $"/handover/records/{handover.Id}", recipients, userId, cancellationToken);
		}
		return handover;
	}

	public async Task<HandoverRecord?> RefreshHandoverAsync(Guid handoverId, Guid userId, CancellationToken cancellationToken = default)
	{
		var handover = await handovers.Find(OrganizationFilter<HandoverRecord>() & Builders<HandoverRecord>.Filter.Eq(item => item.Id, handoverId)).FirstOrDefaultAsync(cancellationToken);
		if (handover is null || handover.Status is HandoverStatus.Completed or HandoverStatus.Cancelled) return null;
		var preserved = handover.Items.Where(item => item.Status != HandoverItemStatus.Pending || item.OutgoingConfirmedAt.HasValue || item.IncomingConfirmedAt.HasValue || item.ItemType is HandoverItemType.AccessTransfer or HandoverItemType.General).ToList();
		var generated = await GenerateItemsAsync(handover.OperationalRoleId, handover.OutgoingUserId.HasValue, handover.IncomingUserId.HasValue, cancellationToken);
		var preservedSources = preserved.Where(item => item.SourceEntityId.HasValue).Select(item => (item.ItemType, item.SourceEntityId)).ToHashSet();
		handover.Items = preserved.Concat(generated.Where(item => !preservedSources.Contains((item.ItemType, item.SourceEntityId)))).Select((item, index) => { item.DisplayOrder = index; return item; }).ToList();
		handover.UpdatedAt = DateTime.UtcNow;
		await ReplaceHandoverAsync(handover, cancellationToken);
		await RecordAuditAsync("Handover", handover.Id, "Refreshed", "Unconfirmed generated items were refreshed from current organization data.", userId, cancellationToken);
		return handover;
	}

	public async Task<IReadOnlyList<HandoverAuditEntry>> GetAuditAsync(Guid entityId, CancellationToken cancellationToken = default) =>
		await audit.Find(OrganizationFilter<HandoverAuditEntry>() & Builders<HandoverAuditEntry>.Filter.Eq(item => item.EntityId, entityId)).SortByDescending(item => item.OccurredAt).ToListAsync(cancellationToken);

	public async Task NotifyDocumentArchivedAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default)
	{
		var links = await documentLinks.Find(OrganizationFilter<HandoverDocumentLink>() & Builders<HandoverDocumentLink>.Filter.Eq(item => item.OrganizationDocumentId, documentId)).ToListAsync(cancellationToken);
		var responsibilityIds = links.Where(link => link.ResponsibilityId.HasValue).Select(link => link.ResponsibilityId!.Value).ToHashSet();
		var linkedResponsibilities = responsibilityIds.Count == 0
			? []
			: await responsibilities.Find(OrganizationFilter<RoleResponsibility>() & Builders<RoleResponsibility>.Filter.In(item => item.Id, responsibilityIds)).ToListAsync(cancellationToken);
		var roleIds = links.Where(link => link.OperationalRoleId.HasValue).Select(link => link.OperationalRoleId!.Value)
			.Concat(linkedResponsibilities.Select(item => item.OperationalRoleId)).Distinct().ToList();
		var linkedRoles = roleIds.Count == 0
			? []
			: await roles.Find(OrganizationFilter<OperationalRole>() & Builders<OperationalRole>.Filter.In(item => item.Id, roleIds)).ToListAsync(cancellationToken);
		var recipients = linkedRoles.SelectMany(role => role.SupportingOwnerUserIds.Append(role.PrimaryOwnerUserId ?? Guid.Empty)).Where(id => id != Guid.Empty).Distinct().ToList();
		if (recipients.Count > 0)
		{
			await NotifyAsync(NotificationType.OrganizationDocumentUnavailable, NotificationSourceType.OrganizationDocument, documentId, "Organisation document archived", "A document linked to an operational role is no longer available for active handovers.", "/handover/documents", recipients, userId, cancellationToken);
		}
		await RecordAuditAsync("OrganizationDocument", documentId, "Archived", $"Archived document affected {links.Count} Handover Vault link(s).", userId, cancellationToken);
	}

	private async Task<List<HandoverItem>> GenerateItemsAsync(Guid roleId, bool outgoing, bool incoming, CancellationToken cancellationToken)
	{
		var roleResponsibilities = await responsibilities.Find(OrganizationFilter<RoleResponsibility>() & Builders<RoleResponsibility>.Filter.Where(item => item.OperationalRoleId == roleId && item.IsActive)).ToListAsync(cancellationToken);
		var responsibilityIds = roleResponsibilities.Select(item => item.Id).ToHashSet();
		var links = await documentLinks.Find(OrganizationFilter<HandoverDocumentLink>() & Builders<HandoverDocumentLink>.Filter.Where(link => link.IsRequiredForHandover && (link.OperationalRoleId == roleId || (link.ResponsibilityId.HasValue && responsibilityIds.Contains(link.ResponsibilityId.Value))))).ToListAsync(cancellationToken);
		var outstandingTasks = await tasks.Find(OrganizationFilter<OperationalTask>() & Builders<OperationalTask>.Filter.Where(item => item.OperationalRoleId == roleId && item.Status != OperationalTaskStatus.Completed && item.Status != OperationalTaskStatus.Cancelled)).ToListAsync(cancellationToken);
		var activeContacts = await contacts.Find(OrganizationFilter<OperationalContact>() & Builders<OperationalContact>.Filter.Where(item => item.IsActive && (item.OperationalRoleId == roleId || (item.ResponsibilityId.HasValue && responsibilityIds.Contains(item.ResponsibilityId.Value))))).ToListAsync(cancellationToken);
		var organizationDocuments = (await documents.GetAllAsync(cancellationToken)).ToDictionary(document => document.Id);
		var items = new List<HandoverItem>();
		foreach (var responsibility in roleResponsibilities) items.Add(NewItem(responsibility.Title, responsibility.Summary, HandoverItemType.Responsibility, responsibility.Id, outgoing, incoming, items.Count));
		foreach (var link in links)
		{
			var title = organizationDocuments.TryGetValue(link.OrganizationDocumentId, out var document) ? document.Title : "Unavailable organization document";
			items.Add(NewItem(title, link.Purpose, HandoverItemType.OrganizationDocument, link.OrganizationDocumentId, outgoing, incoming, items.Count));
		}
		foreach (var task in outstandingTasks) items.Add(NewItem(task.Title, task.Description, HandoverItemType.OutstandingTask, task.Id, outgoing, incoming, items.Count));
		foreach (var contact in activeContacts) items.Add(NewItem(contact.Name, contact.Purpose, HandoverItemType.Contact, contact.Id, outgoing, incoming, items.Count));
		return items;
	}

	private static HandoverItem NewItem(string title, string description, HandoverItemType type, Guid sourceId, bool outgoing, bool incoming, int order) => new()
	{
		Id = Guid.NewGuid(), Title = title, Description = description, ItemType = type, SourceEntityId = sourceId,
		RequiresOutgoingConfirmation = outgoing, RequiresIncomingConfirmation = incoming, DisplayOrder = order
	};

	private static bool IsFullyConfirmed(HandoverItem item) =>
		(!item.RequiresOutgoingConfirmation || item.OutgoingConfirmedAt.HasValue) &&
		(!item.RequiresIncomingConfirmation || item.IncomingConfirmedAt.HasValue);

	private async Task SnapshotDocumentAcknowledgementsAsync(HandoverRecord handover, CancellationToken cancellationToken)
	{
		var allDocuments = (await documents.GetAllAsync(cancellationToken)).ToDictionary(document => document.Id);
		var links = await documentLinks.Find(OrganizationFilter<HandoverDocumentLink>()).ToListAsync(cancellationToken);
		foreach (var item in handover.Items.Where(item => item.ItemType == HandoverItemType.OrganizationDocument && item.SourceEntityId.HasValue))
		{
			item.DocumentTitleAtCompletion = allDocuments.TryGetValue(item.SourceEntityId!.Value, out var document) ? document.Title : item.Title;
			item.DocumentPurposeAtCompletion = links.FirstOrDefault(link => link.OrganizationDocumentId == item.SourceEntityId)?.Purpose ?? item.Description;
			item.AcknowledgedAt = handover.CompletedAt;
		}
	}

	private async Task CreateNextOccurrenceAsync(OperationalTask completed, Guid userId, CancellationToken cancellationToken)
	{
		var exists = await tasks.Find(OrganizationFilter<OperationalTask>() & Builders<OperationalTask>.Filter.Eq(item => item.RecurrenceSourceTaskId, completed.Id)).AnyAsync(cancellationToken);
		if (exists) return;
		var next = new OperationalTask
		{
			Id = Guid.NewGuid(), OrganizationId = tenant.OrganizationId, OperationalRoleId = completed.OperationalRoleId,
			ResponsibilityId = completed.ResponsibilityId, Title = completed.Title, Description = completed.Description,
			DueAt = completed.NextOccurrenceAt, AssignedUserIds = [.. completed.AssignedUserIds], Status = OperationalTaskStatus.NotStarted,
			Recurrence = completed.Recurrence, RecurrenceInterval = completed.RecurrenceInterval, CustomIntervalDays = completed.CustomIntervalDays,
			RecurrenceSourceTaskId = completed.Id, CreatedAt = DateTime.UtcNow, CreatedByUserId = userId, UpdatedAt = DateTime.UtcNow, UpdatedByUserId = userId
		};
		await tasks.InsertOneAsync(next, cancellationToken: cancellationToken);
	}

	private async Task RequireRoleAsync(Guid id, CancellationToken cancellationToken)
	{
		if (id == Guid.Empty || await GetRoleAsync(id, cancellationToken) is null) throw new ArgumentException("Operational role is unavailable.");
	}

	private async Task RequireResponsibilityAsync(Guid id, CancellationToken cancellationToken)
	{
		if (id == Guid.Empty || !await responsibilities.Find(OrganizationFilter<RoleResponsibility>() & Builders<RoleResponsibility>.Filter.Eq(item => item.Id, id)).AnyAsync(cancellationToken)) throw new ArgumentException("Responsibility is unavailable.");
	}

	private async Task ValidateUsersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
	{
		foreach (var id in userIds.Where(id => id != Guid.Empty).Distinct())
		{
			var user = await users.GetByIdAsync(id, cancellationToken);
			if (user is null || !user.IsActive) throw new ArgumentException("An assigned user is not an active member of this organization.");
		}
	}

	private async Task ReplaceHandoverAsync(HandoverRecord handover, CancellationToken cancellationToken) =>
		await handovers.ReplaceOneAsync(OrganizationFilter<HandoverRecord>() & Builders<HandoverRecord>.Filter.Eq(item => item.Id, handover.Id), handover, cancellationToken: cancellationToken);

	private async Task RecordAuditAsync(string entityType, Guid entityId, string action, string detail, Guid userId, CancellationToken cancellationToken) =>
		await audit.InsertOneAsync(new HandoverAuditEntry { Id = Guid.NewGuid(), OrganizationId = tenant.OrganizationId, EntityType = entityType, EntityId = entityId, Action = action, Detail = detail, UserId = userId, OccurredAt = DateTime.UtcNow }, cancellationToken: cancellationToken);

	private async Task NotifyAsync(NotificationType type, NotificationSourceType sourceType, Guid sourceId, string title, string message, string actionPath, IReadOnlyList<Guid> recipients, Guid createdByUserId, CancellationToken cancellationToken)
	{
		var activeRecipients = new List<Guid>();
		foreach (var id in recipients.Distinct())
		{
			if ((await users.GetByIdAsync(id, cancellationToken))?.IsActive == true &&
				!await notifications.ExistsAsync(type, sourceId, id, cancellationToken))
			{
				activeRecipients.Add(id);
			}
		}
		if (activeRecipients.Count == 0) return;
		await notifications.CreateAsync(new ClubNotification
		{
			Type = type, SourceType = sourceType, SourceId = sourceId, Title = title, Message = message, ActionPath = actionPath,
			CreatedByUserId = createdByUserId, Recipients = activeRecipients.Select(id => new ClubNotificationRecipient { UserId = id }).ToList()
		}, cancellationToken);
	}

	private async Task NotifyTaskDeadlinesAsync(IReadOnlyList<OperationalTask> allTasks, Guid userId, CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		foreach (var task in allTasks.Where(task => task.AssignedUserIds.Contains(userId) && task.DueAt.HasValue && task.Status is not OperationalTaskStatus.Completed and not OperationalTaskStatus.Cancelled))
		{
			if (task.DueAt < now)
			{
				await NotifyAsync(NotificationType.OperationalTaskOverdue, NotificationSourceType.OperationalTask, task.Id, "Operational task overdue", task.Title, "/handover/tasks", [userId], task.CreatedByUserId, cancellationToken);
			}
			else if (task.DueAt <= now.AddHours(48))
			{
				await NotifyAsync(NotificationType.OperationalTaskDue, NotificationSourceType.OperationalTask, task.Id, "Operational task due soon", task.Title, "/handover/tasks", [userId], task.CreatedByUserId, cancellationToken);
			}
		}
	}

	private FilterDefinition<T> OrganizationFilter<T>() => Builders<T>.Filter.Eq("OrganizationId", tenant.OrganizationId);

	public static List<ContinuityWarning> BuildWarnings(
		IReadOnlyList<OperationalRole> roles,
		IReadOnlyList<RoleResponsibility> responsibilities,
		IReadOnlyList<HandoverDocumentLink> links,
		IReadOnlyList<OperationalTask> tasks,
		IReadOnlyList<OperationalContact> contacts,
		IReadOnlyList<HandoverRecord> handovers,
		IReadOnlyList<ClubPost> documents)
	{
		var warnings = new List<ContinuityWarning>();
		var documentsById = documents.ToDictionary(document => document.Id);
		foreach (var role in roles.Where(role => role.IsActive))
		{
			if (!role.PrimaryOwnerUserId.HasValue) warnings.Add(Warning("role-no-owner", ContinuityWarningSeverity.Critical, $"{role.Name} has no primary owner.", "OperationalRole", role.Id, $"/handover/roles/{role.Id}"));
			if (role.SupportingOwnerUserIds.Count == 0) warnings.Add(Warning("role-no-support", ContinuityWarningSeverity.Attention, $"{role.Name} has no supporting owner.", "OperationalRole", role.Id, $"/handover/roles/{role.Id}"));
		}
		foreach (var responsibility in responsibilities.Where(item => item.IsActive && item.IsCritical))
		{
			var responsibilityLinks = links.Where(link => link.ResponsibilityId == responsibility.Id).ToList();
			if (responsibilityLinks.Count == 0) warnings.Add(Warning("critical-no-document", ContinuityWarningSeverity.Critical, $"Critical responsibility ‘{responsibility.Title}’ has no linked organization document.", "Responsibility", responsibility.Id, $"/handover/roles/{responsibility.OperationalRoleId}"));
			foreach (var link in responsibilityLinks.Where(link => !documentsById.TryGetValue(link.OrganizationDocumentId, out var document) || document.IsArchived)) warnings.Add(Warning("document-unavailable", ContinuityWarningSeverity.Critical, $"A required document for ‘{responsibility.Title}’ is archived or unavailable.", "OrganizationDocument", link.OrganizationDocumentId, "/handover/documents"));
		}
		foreach (var link in links.Where(link => link.IsRequiredForHandover && (!documentsById.TryGetValue(link.OrganizationDocumentId, out var document) || document.IsArchived)))
		{
			if (warnings.Any(warning => warning.Code == "document-unavailable" && warning.EntityId == link.OrganizationDocumentId)) continue;
			warnings.Add(Warning("document-unavailable", ContinuityWarningSeverity.Critical, "A document required for handover is archived or unavailable.", "OrganizationDocument", link.OrganizationDocumentId, "/handover/documents"));
		}
		foreach (var task in tasks.Where(task => task.DueAt < DateTime.UtcNow && task.Status is not OperationalTaskStatus.Completed and not OperationalTaskStatus.Cancelled)) warnings.Add(Warning("task-overdue", ContinuityWarningSeverity.Attention, $"Task ‘{task.Title}’ is overdue.", "OperationalTask", task.Id, "/handover/tasks"));
		foreach (var contact in contacts.Where(contact => contact.IsActive && string.IsNullOrWhiteSpace(contact.Purpose))) warnings.Add(Warning("contact-no-purpose", ContinuityWarningSeverity.Attention, $"Contact ‘{contact.Name}’ has no clear purpose.", "OperationalContact", contact.Id, contact.OperationalRoleId.HasValue ? $"/handover/roles/{contact.OperationalRoleId}" : "/handover/roles"));
		foreach (var handover in handovers.Where(item => item.Status is not HandoverStatus.Completed and not HandoverStatus.Cancelled))
		{
			warnings.Add(Warning("handover-incomplete", ContinuityWarningSeverity.Attention, "A formal handover remains incomplete.", "Handover", handover.Id, $"/handover/records/{handover.Id}"));
			if (handover.DueAt < DateTime.UtcNow) warnings.Add(Warning("handover-overdue", ContinuityWarningSeverity.Critical, "A formal handover is overdue.", "Handover", handover.Id, $"/handover/records/{handover.Id}"));
			if (handover.Items.Any(item => item.Status == HandoverItemStatus.Blocked)) warnings.Add(Warning("handover-blocked", ContinuityWarningSeverity.Critical, "A handover contains a blocked checklist item.", "Handover", handover.Id, $"/handover/records/{handover.Id}"));
			if (handover.Items.Any(item => item.ItemType == HandoverItemType.OrganizationDocument && item.RequiresIncomingConfirmation && !item.IncomingConfirmedAt.HasValue)) warnings.Add(Warning("incoming-documents-unconfirmed", ContinuityWarningSeverity.Attention, "The incoming owner has not confirmed required organisation documents.", "Handover", handover.Id, $"/handover/records/{handover.Id}"));
			if (handover.Items.Any(item => item.ItemType == HandoverItemType.OutstandingTask && item.RequiresOutgoingConfirmation && !item.OutgoingConfirmedAt.HasValue)) warnings.Add(Warning("outgoing-work-unconfirmed", ContinuityWarningSeverity.Attention, "The outgoing owner has not confirmed outstanding work.", "Handover", handover.Id, $"/handover/records/{handover.Id}"));
		}
		return warnings;
	}

	private static ContinuityWarning Warning(string code, ContinuityWarningSeverity severity, string message, string type, Guid id, string path) => new() { Code = code, Severity = severity, Message = message, EntityType = type, EntityId = id, ActionPath = path };
}
