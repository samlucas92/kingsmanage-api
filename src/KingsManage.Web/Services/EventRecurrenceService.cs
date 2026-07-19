using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class EventRecurrenceService : IEventRecurrenceService
{
	private const int MaxRecurringOccurrences = 80;

	public string BuildTrainingTitle(DateTime startDateTime)
	{
		return startDateTime == default
			? "Training"
			: $"Training {startDateTime:dd/MM/yyyy}";
	}

	public string? Validate(
		ClubEventType type,
		DateTime startDateTime,
		CreateEventRecurrenceModel? recurrence)
	{
		if (recurrence?.IsRecurring != true)
		{
			return null;
		}

		if (type == ClubEventType.Match)
		{
			return "Recurring match events are not supported yet.";
		}

		if (recurrence.Interval < 1)
		{
			return "Recurring interval must be at least 1.";
		}

		if (recurrence.Unit is not RecurrenceIntervalUnit.Days and not RecurrenceIntervalUnit.Weeks)
		{
			return "Recurring interval unit must be days or weeks.";
		}

		if (recurrence.EndDate == default)
		{
			return "Recurring events need an end date.";
		}

		if (recurrence.EndDate.Date < startDateTime.Date)
		{
			return "Recurring end date cannot be before the event start date.";
		}

		var occurrences = BuildOccurrenceStartDates(
			startDateTime,
			recurrence.EndDate,
			recurrence.Interval,
			recurrence.Unit);

		if (occurrences.Count > MaxRecurringOccurrences)
		{
			return $"Recurring events are limited to {MaxRecurringOccurrences} occurrences.";
		}

		return null;
	}

	public List<ClubEvent> BuildOccurrences(
		ClubEvent sourceEvent,
		CreateEventRecurrenceModel? recurrence)
	{
		if (recurrence?.IsRecurring != true)
		{
			return [sourceEvent];
		}

		var occurrenceStartDates = BuildOccurrenceStartDates(
			sourceEvent.StartDateTime,
			recurrence.EndDate,
			recurrence.Interval,
			recurrence.Unit);
		var seriesId = Guid.NewGuid();
		var duration = sourceEvent.EndDateTime.HasValue
			? sourceEvent.EndDateTime.Value - sourceEvent.StartDateTime
			: (TimeSpan?)null;

		return occurrenceStartDates
			.Select((startDateTime, index) => new ClubEvent
			{
				Id = index == 0 && sourceEvent.Id != Guid.Empty ? sourceEvent.Id : Guid.NewGuid(),
				Type = sourceEvent.Type,
				TeamScope = sourceEvent.TeamScope,
				TeamIds = [..sourceEvent.TeamIds],
				Title = sourceEvent.Type == ClubEventType.Training
					? BuildTrainingTitle(startDateTime)
					: sourceEvent.Title,
				Description = sourceEvent.Description,
				StartDateTime = startDateTime,
				EndDateTime = duration.HasValue ? startDateTime + duration.Value : null,
				Location = sourceEvent.Location,
				RecurrenceSeriesId = seriesId,
				Recurrence = new ClubEventRecurrence
				{
					SeriesId = seriesId,
					OccurrenceNumber = index + 1,
					TotalOccurrences = occurrenceStartDates.Count,
					Interval = recurrence.Interval,
					Unit = recurrence.Unit,
					SeriesStartDateTime = sourceEvent.StartDateTime,
					SeriesEndDate = recurrence.EndDate.Date
				},
				MatchLinks = [],
				AvailabilityResponses = [],
				SeenBy = []
			})
			.ToList();
	}

	private static List<DateTime> BuildOccurrenceStartDates(
		DateTime startDateTime,
		DateTime endDate,
		int interval,
		RecurrenceIntervalUnit unit)
	{
		var dates = new List<DateTime>();
		var current = startDateTime;

		while (current.Date <= endDate.Date && dates.Count <= MaxRecurringOccurrences)
		{
			dates.Add(current);
			current = unit == RecurrenceIntervalUnit.Weeks
				? current.AddDays(interval * 7)
				: current.AddDays(interval);
		}

		return dates;
	}
}
