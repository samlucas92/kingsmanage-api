namespace KingsManage;

public sealed class SportsClub
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid OrganizationId { get; set; }

	public string Name { get; set; } = string.Empty;

	public string Slug { get; set; } = string.Empty;

	public string SportKey { get; set; } = string.Empty;

	public string PrimaryColor { get; set; } = "#0f766e";

	public string SecondaryColor { get; set; } = "#d9f99d";

	public string ContactEmail { get; set; } = string.Empty;

	public string ContactPhone { get; set; } = string.Empty;

	public string WebsiteUrl { get; set; } = string.Empty;

	public List<ClubVenue> Venues { get; set; } = [];

	public int SetupStep { get; set; }

	public DateTime? SetupCompletedAt { get; set; }

	public List<ClubFormation> CustomFormations { get; set; } = [];

	public Guid? LogoFileId { get; set; }

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ClubVenue
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public string Name { get; set; } = string.Empty;

	public string Address { get; set; } = string.Empty;

	public string MapUrl { get; set; } = string.Empty;

	public bool IsDefault { get; set; }
}

public sealed class ClubFormation
{
	public string Key { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public List<ClubFormationSlot> Slots { get; set; } = [];
}

public sealed class ClubFormationSlot
{
	public string Key { get; set; } = string.Empty;

	public string Label { get; set; } = string.Empty;

	public double X { get; set; }

	public double Y { get; set; }
}
