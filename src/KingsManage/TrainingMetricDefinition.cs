namespace KingsManage;

public sealed class TrainingMetricDefinition
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public List<TrainingMetricCategoryDefinition> Categories { get; set; } = [];
}

public sealed class TrainingMetricCategoryDefinition
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
}
