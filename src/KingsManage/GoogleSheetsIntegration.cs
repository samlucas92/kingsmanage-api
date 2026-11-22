namespace KingsManage;

public sealed class OrganizationGoogleSheetsIntegration
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public OrganizationIntegrationStatus Status { get; set; }
	public string? ConnectedGoogleUserEmail { get; set; }
	public string EncryptedRefreshToken { get; set; } = string.Empty;
	public string? SpreadsheetId { get; set; }
	public string? SpreadsheetName { get; set; }
	public string? SpreadsheetUrl { get; set; }
	public List<GoogleSheetTab> Sheets { get; set; } = [];
	public DateTime? LastValidatedAt { get; set; }
	public string? LastError { get; set; }
	public Guid CreatedByUserId { get; set; }
	public Guid UpdatedByUserId { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}

public sealed class GoogleSheetTab
{
	public int SheetId { get; set; }
	public string Title { get; set; } = string.Empty;
	public int Index { get; set; }
}

public sealed class GoogleSheetsOAuthState
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public Guid UserId { get; set; }
	public string StateHash { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
	public DateTime? UsedAt { get; set; }
}

public interface IOrganizationGoogleSheetsIntegrationService
{
	Task<OrganizationGoogleSheetsIntegration?> GetCurrentAsync(CancellationToken cancellationToken = default);
	Task<OrganizationGoogleSheetsIntegration> SaveAsync(OrganizationGoogleSheetsIntegration integration, CancellationToken cancellationToken = default);
	Task<bool> DisconnectAsync(CancellationToken cancellationToken = default);
	Task StoreOAuthStateAsync(GoogleSheetsOAuthState state, CancellationToken cancellationToken = default);
	Task<bool> ConsumeOAuthStateAsync(Guid userId, string stateHash, CancellationToken cancellationToken = default);
}
