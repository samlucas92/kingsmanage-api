namespace KingsManage;

public class ClubFormQuestionOption
{
	public string Value { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public Guid? PlayerId { get; set; }
	public bool RequiresTextInput { get; set; }
	public string TextInputLabel { get; set; } = string.Empty;
}
