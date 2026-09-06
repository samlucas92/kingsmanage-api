namespace KingsManage;

public class MatchTimelineEvent
{
	public Guid Id { get; set; }
	public MatchTimelineEventType Type { get; set; }
	public int Minute { get; set; }
	public Guid PlayerId { get; set; }
	public Guid? SecondaryPlayerId { get; set; }
}

public enum MatchTimelineEventType
{
	Goal,
	YellowCard,
	RedCard,
	Substitution
}
