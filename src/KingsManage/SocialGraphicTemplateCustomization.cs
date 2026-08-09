namespace KingsManage;

public class SocialGraphicTemplateCustomization : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public string TemplateId { get; set; } = string.Empty;
	public int SchemaVersion { get; set; } = 1;
	public string DefinitionJson { get; set; } = "{}";
	public int Revision { get; set; }
	public Guid UpdatedByUserId { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
