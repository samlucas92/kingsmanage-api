namespace KingsManage;

public class Player
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public int Number { get; set; }
	public List<string> Positions { get; set; } = [];
	public int Appearances { get; set; }
	public int Goals { get; set; }
	public bool IsActive { get; set; } = true;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
