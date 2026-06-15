using KingsManage;

namespace KingsManage.Web.Models;

public sealed class CreateClubPostModel
{
	public ClubPostType Type { get; set; } = ClubPostType.General;
	public string Title { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
	public bool IsPinned { get; set; }

	public ClubPost ToClubPost(Guid createdByUserId, string createdByUserEmail)
	{
		return new ClubPost
		{
			Type = Type,
			Title = Title,
			Body = Body,
			IsPinned = IsPinned,
			CreatedByUserId = createdByUserId,
			CreatedByUserEmail = createdByUserEmail,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
	}
}
