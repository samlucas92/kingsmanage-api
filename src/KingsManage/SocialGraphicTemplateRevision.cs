namespace KingsManage;

public class SocialGraphicTemplateRevision : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public Guid CustomizationId { get; set; }
	public string TemplateId { get; set; } = string.Empty;
	public int SchemaVersion { get; set; } = 1;
	public string DefinitionJson { get; set; } = "{}";
	public int Revision { get; set; }
	public Guid CreatedByUserId { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
