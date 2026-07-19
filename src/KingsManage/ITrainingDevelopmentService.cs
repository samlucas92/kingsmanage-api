namespace KingsManage;

public interface ITrainingDevelopmentService
{
	IReadOnlyList<TrainingMetricDefinition> GetMetricDefinitions(TrainingPlayerRole playerRole);

	Task<IReadOnlyList<TrainingAssessment>> GetEventAssessmentsAsync(
		Guid eventId,
		CancellationToken cancellationToken = default);

	Task<TrainingAssessment?> GetPlayerEventAssessmentAsync(
		Guid eventId,
		Guid playerId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<TrainingAssessment>> GetPlayerAssessmentsAsync(
		Guid playerId,
		DateTime? from,
		DateTime? to,
		CancellationToken cancellationToken = default);

	Task<TrainingAssessment> UpsertAsync(
		TrainingAssessment assessment,
		CancellationToken cancellationToken = default);

	Task DeleteEventAssessmentsAsync(
		Guid eventId,
		CancellationToken cancellationToken = default);
}
