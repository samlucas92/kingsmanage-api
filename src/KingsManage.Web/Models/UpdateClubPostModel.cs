using KingsManage;

namespace KingsManage.Web.Models;

public sealed class UpdateClubPostModel
{
	public ClubPostType Type { get; set; } = ClubPostType.General;
	public string Title { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
	public bool IsPinned { get; set; }

	public ClubPost ToClubPost(ClubPost existingPost)
	{
		existingPost.Type = Type;
		existingPost.Title = Title;
		existingPost.Body = Body;
		existingPost.IsPinned = IsPinned;
		existingPost.UpdatedAt = DateTime.UtcNow;

		return existingPost;
	}
}
