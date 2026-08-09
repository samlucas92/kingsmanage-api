namespace KingsManage;

public interface ISocialGraphicTemplateService
{
	Task<SocialGraphicTemplateCustomization?> GetAsync(
		string templateId,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<SocialGraphicTemplateRevision>> GetRevisionsAsync(
		string templateId,
		int limit = 20,
		CancellationToken cancellationToken = default
	);

	Task<SocialGraphicTemplateSaveResult> SaveAsync(
		string templateId,
		int schemaVersion,
		string definitionJson,
		int expectedRevision,
		Guid userId,
		CancellationToken cancellationToken = default
	);

	Task<SocialGraphicTemplateSaveResult> RestoreRevisionAsync(
		string templateId,
		int revision,
		int expectedRevision,
		Guid userId,
		CancellationToken cancellationToken = default
	);

	Task<SocialGraphicTemplateResetResult> ResetAsync(
		string templateId,
		int expectedRevision,
		CancellationToken cancellationToken = default
	);
}

public sealed record SocialGraphicTemplateSaveResult(
	SocialGraphicTemplateSaveStatus Status,
	SocialGraphicTemplateCustomization? Customization = null
);

public enum SocialGraphicTemplateSaveStatus
{
	Saved,
	Conflict,
	RevisionNotFound
}

public enum SocialGraphicTemplateResetResult
{
	Reset,
	NotFound,
	Conflict
}
