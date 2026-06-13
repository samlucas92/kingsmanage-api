namespace KingsManage.Web.Models;

public class SeasonSetupRequest
{
	public string Name { get; set; } = string.Empty;

	public DateTime StartDate { get; set; }

	public DateTime EndDate { get; set; }

	public bool MakeActive { get; set; }

	public bool SetStartingFinanceAmount { get; set; }

	public decimal StartingFinanceAmount { get; set; }
}
