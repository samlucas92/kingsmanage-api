namespace KingsManage;

public class MatchPlayerStats
{
	public Guid PlayerId { get; set; }
	public int Goals { get; set; }
	public int Assists { get; set; }
	public int YellowCards { get; set; }
	public int RedCards { get; set; }
	public int Minutes { get; set; }
	public bool IsMOTM { get; set; }
	public string Note { get; set; } = string.Empty;
}