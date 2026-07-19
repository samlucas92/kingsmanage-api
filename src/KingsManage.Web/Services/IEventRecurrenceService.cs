using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface IEventRecurrenceService
{
	string BuildTrainingTitle(DateTime startDateTime);

	string? Validate(
		ClubEventType type,
		DateTime startDateTime,
		CreateEventRecurrenceModel? recurrence);

	List<ClubEvent> BuildOccurrences(
		ClubEvent sourceEvent,
		CreateEventRecurrenceModel? recurrence);
}
