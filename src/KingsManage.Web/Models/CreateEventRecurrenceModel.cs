using KingsManage;

namespace KingsManage.Web.Models;

public class CreateEventRecurrenceModel
{
	public bool IsRecurring { get; set; }
	public int Interval { get; set; } = 1;
	public RecurrenceIntervalUnit Unit { get; set; } = RecurrenceIntervalUnit.Weeks;
	public DateTime EndDate { get; set; }
}
