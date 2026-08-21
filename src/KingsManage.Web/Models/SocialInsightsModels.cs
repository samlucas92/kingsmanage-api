using KingsManage;

namespace KingsManage.Web.Models;

public sealed class SocialInsightsOverview
{
	public DateTime GeneratedAt { get; set; }
	public IReadOnlyList<SocialAccountInsights> Accounts { get; set; } = [];
	public IReadOnlyList<SocialPostInsightsSummary> Posts { get; set; } = [];
}

public sealed class SocialAccountInsights
{
	public SocialPlatform Platform { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Username { get; set; }
	public long? FollowerCount { get; set; }
	public long? PostCount { get; set; }
}

public class SocialPostInsightsSummary
{
	public SocialPlatform Platform { get; set; }
	public string Id { get; set; } = string.Empty;
	public string Caption { get; set; } = string.Empty;
	public string? MediaType { get; set; }
	public DateTime CreatedAt { get; set; }
	public string? Permalink { get; set; }
	public string? ThumbnailUrl { get; set; }
	public long? LikeCount { get; set; }
	public long? CommentCount { get; set; }
	public long? ShareCount { get; set; }
}

public sealed class SocialPostInsightsDetail : SocialPostInsightsSummary
{
	public IReadOnlyDictionary<string, long> Metrics { get; set; } = new Dictionary<string, long>();

	public static SocialPostInsightsDetail From(SocialPostInsightsSummary summary, IReadOnlyDictionary<string, long> metrics) => new()
	{
		Platform = summary.Platform,
		Id = summary.Id,
		Caption = summary.Caption,
		MediaType = summary.MediaType,
		CreatedAt = summary.CreatedAt,
		Permalink = summary.Permalink,
		ThumbnailUrl = summary.ThumbnailUrl,
		LikeCount = summary.LikeCount,
		CommentCount = summary.CommentCount,
		ShareCount = summary.ShareCount,
		Metrics = metrics
	};
}
