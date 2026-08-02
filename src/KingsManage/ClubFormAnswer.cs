namespace KingsManage;

public class ClubFormAnswer
{
	public Guid QuestionId { get; set; }
	public string TextValue { get; set; } = string.Empty;
	public List<string> SelectedOptions { get; set; } = [];
	public int? RatingValue { get; set; }
	public bool? BooleanValue { get; set; }
}
