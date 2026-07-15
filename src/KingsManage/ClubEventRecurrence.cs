namespace KingsManage;

public class ClubEventRecurrence
{
	public Guid SeriesId { get; set; }
	public int OccurrenceNumber { get; set; }
	public int TotalOccurrences { get; set; }
	public int Interval { get; set; }
	public RecurrenceIntervalUnit Unit { get; set; } = RecurrenceIntervalUnit.Weeks;
	public DateTime SeriesStartDateTime { get; set; }
	public DateTime SeriesEndDate { get; set; }
}

public enum RecurrenceIntervalUnit
{
	Days,
	Weeks
}
