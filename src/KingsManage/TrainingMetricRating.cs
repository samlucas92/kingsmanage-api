namespace KingsManage;

public class TrainingMetricRating
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public int Rating { get; set; }
	public List<TrainingMetricCategoryRating> Categories { get; set; } = [];
}
