using KingsManage;

namespace KingsManage.Web.Models;

public sealed class PlayerSponsorshipViewModel
{
	public Guid Id { get; set; }
	public Guid PlayerId { get; set; }
	public string PlayerName { get; set; } = string.Empty;
	public string GoCode { get; set; } = string.Empty;
	public PlayerSponsorshipStatus Status { get; set; }
	public string ClubName { get; set; } = string.Empty;
	public string ClubPrimaryColor { get; set; } = string.Empty;
	public string ClubSecondaryColor { get; set; } = string.Empty;
	public string SponsorName { get; set; } = string.Empty;
	public string SponsorDescription { get; set; } = string.Empty;
	public string SponsorImageDataUrl { get; set; } = string.Empty;
	public bool HasImage { get; set; }
	public bool TermsAccepted { get; set; }
	public DateTime? TermsAcceptedAt { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
	public string TermsText { get; set; } = PlayerSponsorshipTerms.Text;

	public static PlayerSponsorshipViewModel FromSponsorship(PlayerSponsorship item, bool includeImage = true) => new()
	{
		Id = item.Id,
		PlayerId = item.PlayerId,
		PlayerName = item.PlayerName,
		GoCode = item.GoCode,
		Status = item.Status,
		ClubName = item.ClubName,
		ClubPrimaryColor = item.ClubPrimaryColor,
		ClubSecondaryColor = item.ClubSecondaryColor,
		SponsorName = item.SponsorName,
		SponsorDescription = item.SponsorDescription,
		SponsorImageDataUrl = includeImage ? item.SponsorImageDataUrl : string.Empty,
		HasImage = !string.IsNullOrWhiteSpace(item.SponsorImageDataUrl),
		TermsAccepted = item.TermsAccepted,
		TermsAcceptedAt = item.TermsAcceptedAt,
		SubmittedAt = item.SubmittedAt,
		CreatedAt = item.CreatedAt,
		UpdatedAt = item.UpdatedAt,
		TermsText = string.IsNullOrWhiteSpace(item.AcceptedTermsText)
			? PlayerSponsorshipTerms.Text
			: item.AcceptedTermsText
	};
}

public sealed class CreatePlayerSponsorshipModel
{
	public Guid PlayerId { get; set; }
}

public sealed class UpdatePlayerSponsorshipModel
{
	public string SponsorName { get; set; } = string.Empty;
	public string SponsorDescription { get; set; } = string.Empty;
	public string SponsorImageDataUrl { get; set; } = string.Empty;
	public PlayerSponsorshipStatus Status { get; set; }
}

public sealed class SubmitPlayerSponsorshipModel
{
	public string SponsorName { get; set; } = string.Empty;
	public string SponsorDescription { get; set; } = string.Empty;
	public string SponsorImageDataUrl { get; set; } = string.Empty;
	public bool TermsAccepted { get; set; }
}

public static class PlayerSponsorshipTerms
{
	public const string Text = "I confirm that I am authorised to provide this sponsor information and image. I understand that the sponsorship arrangement is made directly with the player and that the club is not liable for any promises, payments, services or other terms agreed between the sponsor and the player.";
}
