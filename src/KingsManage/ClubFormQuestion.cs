namespace KingsManage;

public class ClubFormQuestion
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Prompt { get; set; } = string.Empty;
	public ClubFormQuestionType Type { get; set; } = ClubFormQuestionType.ShortText;
	public bool IsRequired { get; set; }
	public ClubFormQuestionOptionSource OptionSource { get; set; } = ClubFormQuestionOptionSource.Manual;
	public List<string> Options { get; set; } = [];
	public List<ClubFormQuestionOption> ChoiceOptions { get; set; } = [];
	public int MinRating { get; set; } = 1;
	public int MaxRating { get; set; } = 5;
}
