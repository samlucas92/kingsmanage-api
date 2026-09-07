namespace KingsManage;

public sealed class OppositionTeam : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Location { get; set; } = string.Empty;
	public Guid? BadgeFileId { get; set; }
	public bool IsActive { get; set; } = true;
	public string NormalizedName { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
