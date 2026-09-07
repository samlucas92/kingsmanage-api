using KingsManage;

namespace KingsManage.Web.Models;

public sealed class SaveOppositionTeamModel
{
	public string Name { get; set; } = string.Empty;
	public string Location { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;

	public OppositionTeam ToTeam(Guid id = default) => new()
	{
		Id = id,
		Name = Name,
		Location = Location,
		IsActive = IsActive
	};
}
