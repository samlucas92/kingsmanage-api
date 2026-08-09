using KingsManage;

namespace KingsManage.Web.Models;

public sealed class PlatformOrganizationViewModel
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Slug { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
	public IReadOnlyList<OrganizationAdministratorAccount> Administrators { get; set; } = [];

	public static PlatformOrganizationViewModel FromOrganization(
		Organization organization,
		IReadOnlyList<OrganizationAdministratorAccount> administrators) =>
		new()
		{
			Id = organization.Id,
			Name = organization.Name,
			Slug = organization.Slug,
			IsActive = organization.IsActive,
			CreatedAt = organization.CreatedAt,
			UpdatedAt = organization.UpdatedAt,
			Administrators = administrators
		};
}
