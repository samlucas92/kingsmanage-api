using KingsManage;

namespace KingsManage.Web.Models;

public sealed class CreateFileUploadUrlModel
{
	public string OriginalFileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long SizeBytes { get; set; }
	public ClubFileLinkedEntityType LinkedEntityType { get; set; } = ClubFileLinkedEntityType.Post;
	public Guid LinkedEntityId { get; set; }
	public ClubFileVisibility Visibility { get; set; } = ClubFileVisibility.AuthenticatedUsers;
}
