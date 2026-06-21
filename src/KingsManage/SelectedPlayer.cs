namespace KingsManage;

public class SelectedPlayer
{
	public Guid PlayerId { get; set; }
	// Coordinates are optional custom drag-and-drop overrides. Standard lineup
	// placement is resolved from PositionKey and the active sport definition.
	public decimal? X { get; set; }
	public decimal? Y { get; set; }
	public string Area { get; set; } = "pitch";
	public string? PositionKey { get; set; }
	public int? PositionIndex { get; set; }
}
