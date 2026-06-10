namespace KingsManage;

public class Player
{
	public string Id { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public int Number { get; set; }

	public List<string> Positions { get; set; } = [];

	public int Appearances { get; set; }

	public int Goals { get; set; }

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}