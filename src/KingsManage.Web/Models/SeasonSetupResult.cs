using KingsManage;

namespace KingsManage.Web.Models;

public class SeasonSetupResult
{
	public Season Season { get; set; } = new();

	public bool CreatedSeason { get; set; }

	public int FinanceChargesCreatedOrUpdated { get; set; }
}
