namespace KingsManage;

public class TrainingPlanDrill
{
	public Guid Id { get; set; }
	public string Title { get; set; } = string.Empty;
	public int DurationMinutes { get; set; }
	public string Content { get; set; } = string.Empty;
}
