using KingsManage;

namespace KingsManage.Web.Models;

public sealed class MetaIntegrationViewModel
{
	public bool IsConfigured { get; set; }
	public bool IsEnabled { get; set; }
	public OrganizationIntegrationStatus Status { get; set; }
	public string? ConnectedMetaUserName { get; set; }
	public DateTime? TokenExpiresAt { get; set; }
	public DateTime? LastValidatedAt { get; set; }
	public string? LastError { get; set; }
	public string TimeZoneId { get; set; } = "Europe/London";
	public IReadOnlyList<MetaPageViewModel> Pages { get; set; } = [];
	public IReadOnlyList<SocialChannelMapping> ClubMappings { get; set; } = [];
}

public sealed class MetaPageViewModel
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public IReadOnlyList<string> Tasks { get; set; } = [];
	public MetaInstagramAccount? InstagramAccount { get; set; }
}

public sealed class CompleteMetaConnectionRequest
{
	public string Code { get; set; } = string.Empty;
	public string State { get; set; } = string.Empty;
}

public sealed class UpdateMetaConfigurationRequest
{
	public bool IsEnabled { get; set; }
	public string TimeZoneId { get; set; } = "Europe/London";
	public List<SocialChannelMapping> ClubMappings { get; set; } = [];
}

public sealed class SetIntegrationEnabledRequest
{
	public bool IsEnabled { get; set; }
}

public sealed class CreateSocialPublicationRequest
{
	public bool PublishToFacebook { get; set; }
	public bool PublishToInstagram { get; set; }
	public string FacebookCaption { get; set; } = string.Empty;
	public string InstagramCaption { get; set; } = string.Empty;
	public DateTime? ScheduledForUtc { get; set; }
}

public sealed class AttachSocialPublicationFileRequest
{
	public Guid FileId { get; set; }
}
