namespace KingsManage;

public class SelectedPlayer
{
	public Guid PlayerId { get; set; }
	public decimal X { get; set; }
	public decimal Y { get; set; }
	public string Area { get; set; } = "pitch";
	public int? PositionIndex { get; set; }
}
