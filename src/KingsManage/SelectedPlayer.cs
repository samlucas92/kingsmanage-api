namespace KingsManage;

public class SelectedPlayer
{
	public string PlayerId { get; set; } = string.Empty;

	public decimal X { get; set; }

	public decimal Y { get; set; }

	public string Area { get; set; } = "pitch";

	public int? PositionIndex { get; set; }
}