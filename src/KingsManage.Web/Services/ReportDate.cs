namespace KingsManage.Web.Services;

internal static class ReportDate
{
	public static DateTime MonthStart(DateTime date)
	{
		return new DateTime(
			date.Year,
			date.Month,
			1,
			0,
			0,
			0,
			DateTimeKind.Utc);
	}
}
