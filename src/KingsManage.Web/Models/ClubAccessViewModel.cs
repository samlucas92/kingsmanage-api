namespace KingsManage.Web.Models;

public sealed class ClubAccessViewModel
{
	public Guid Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public string SportKey { get; set; } = string.Empty;

	public bool IsCurrent { get; set; }
}
