namespace KingsManage;

public interface ISeasonRolloverStore
{
	Task<SeasonRollover?> GetBySeasonIdAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default);

	Task<SeasonRollover> ApplyAsync(
		SeasonRollover rollover,
		CancellationToken cancellationToken = default);
}
