namespace KingsManage.Web.Models;

public sealed class SocialGraphicTemplateResponse
{
	public string TemplateId { get; set; } = string.Empty;
	public SocialGraphicTemplateCustomizationViewModel? Customization { get; set; }
}

public sealed class SocialGraphicTemplateCustomizationViewModel
{
	public Guid Id { get; set; }
	public string TemplateId { get; set; } = string.Empty;
	public int SchemaVersion { get; set; }
	public string DefinitionJson { get; set; } = string.Empty;
	public int Revision { get; set; }
	public Guid UpdatedByUserId { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}

public sealed class SocialGraphicTemplateRevisionViewModel
{
	public int Revision { get; set; }
	public int SchemaVersion { get; set; }
	public string DefinitionJson { get; set; } = string.Empty;
	public Guid CreatedByUserId { get; set; }
	public DateTime CreatedAt { get; set; }
}

public sealed class SaveSocialGraphicTemplateRequest
{
	public int SchemaVersion { get; set; }
	public string DefinitionJson { get; set; } = string.Empty;
	public int ExpectedRevision { get; set; }
}

public sealed class RestoreSocialGraphicTemplateRevisionRequest
{
	public int ExpectedRevision { get; set; }
}
