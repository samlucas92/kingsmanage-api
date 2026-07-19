using KingsManage;

namespace KingsManage.Web.Models;

public sealed class TrainingMetricDefinitionViewModel
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public List<TrainingMetricCategoryDefinitionViewModel> Categories { get; set; } = [];

	public static TrainingMetricDefinitionViewModel FromDefinition(TrainingMetricDefinition definition)
	{
		return new TrainingMetricDefinitionViewModel
		{
			Key = definition.Key,
			Label = definition.Label,
			Categories = definition.Categories
				.Select(TrainingMetricCategoryDefinitionViewModel.FromDefinition)
				.ToList()
		};
	}
}

public sealed class TrainingMetricCategoryDefinitionViewModel
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;

	public static TrainingMetricCategoryDefinitionViewModel FromDefinition(TrainingMetricCategoryDefinition definition)
	{
		return new TrainingMetricCategoryDefinitionViewModel
		{
			Key = definition.Key,
			Label = definition.Label
		};
	}
}

public sealed class TrainingAssessmentViewModel
{
	public Guid Id { get; set; }
	public Guid EventId { get; set; }
	public Guid PlayerId { get; set; }
	public TrainingPlayerRole PlayerRole { get; set; }
	public List<TrainingMetricRatingViewModel> Metrics { get; set; } = [];
	public string Notes { get; set; } = string.Empty;
	public DateTime AssessedAt { get; set; }
	public DateTime UpdatedAt { get; set; }

	public static TrainingAssessmentViewModel FromAssessment(TrainingAssessment assessment)
	{
		return new TrainingAssessmentViewModel
		{
			Id = assessment.Id,
			EventId = assessment.EventId,
			PlayerId = assessment.PlayerId,
			PlayerRole = assessment.PlayerRole,
			Metrics = assessment.Metrics.Select(TrainingMetricRatingViewModel.FromRating).ToList(),
			Notes = assessment.Notes,
			AssessedAt = assessment.AssessedAt,
			UpdatedAt = assessment.UpdatedAt
		};
	}
}

public sealed class TrainingMetricRatingViewModel
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public int Rating { get; set; }
	public List<TrainingMetricCategoryRatingViewModel> Categories { get; set; } = [];

	public static TrainingMetricRatingViewModel FromRating(TrainingMetricRating rating)
	{
		return new TrainingMetricRatingViewModel
		{
			Key = rating.Key,
			Label = rating.Label,
			Rating = rating.Rating,
			Categories = rating.Categories.Select(TrainingMetricCategoryRatingViewModel.FromRating).ToList()
		};
	}
}

public sealed class TrainingMetricCategoryRatingViewModel
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public int Rating { get; set; }

	public static TrainingMetricCategoryRatingViewModel FromRating(TrainingMetricCategoryRating rating)
	{
		return new TrainingMetricCategoryRatingViewModel
		{
			Key = rating.Key,
			Label = rating.Label,
			Rating = rating.Rating
		};
	}
}

public sealed class SaveTrainingAssessmentModel
{
	public TrainingPlayerRole PlayerRole { get; set; }
	public List<TrainingMetricRatingModel> Metrics { get; set; } = [];
	public string Notes { get; set; } = string.Empty;

	public TrainingAssessment ToAssessment(Guid eventId, Guid playerId, Guid assessedByUserId)
	{
		return new TrainingAssessment
		{
			EventId = eventId,
			PlayerId = playerId,
			PlayerRole = PlayerRole,
			Metrics = Metrics.Select(metric => metric.ToRating()).ToList(),
			Notes = Notes,
			AssessedByUserId = assessedByUserId
		};
	}
}

public sealed class TrainingMetricRatingModel
{
	public string Key { get; set; } = string.Empty;
	public int Rating { get; set; }
	public List<TrainingMetricCategoryRatingModel> Categories { get; set; } = [];

	public TrainingMetricRating ToRating()
	{
		return new TrainingMetricRating
		{
			Key = Key,
			Rating = Rating,
			Categories = Categories.Select(category => category.ToRating()).ToList()
		};
	}
}

public sealed class TrainingMetricCategoryRatingModel
{
	public string Key { get; set; } = string.Empty;
	public int Rating { get; set; }

	public TrainingMetricCategoryRating ToRating()
	{
		return new TrainingMetricCategoryRating
		{
			Key = Key,
			Rating = Rating
		};
	}
}

public sealed class PlayerTrainingDevelopmentViewModel
{
	public Guid PlayerId { get; set; }
	public TrainingPlayerRole PlayerRole { get; set; }
	public int AssessmentCount { get; set; }
	public TrainingAssessmentViewModel? LatestAssessment { get; set; }
	public List<TrainingMetricAverageViewModel> Averages { get; set; } = [];
	public List<TrainingAssessmentViewModel> RecentAssessments { get; set; } = [];
}

public sealed class TrainingMetricAverageViewModel
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public double Rating { get; set; }
	public List<TrainingMetricCategoryAverageViewModel> Categories { get; set; } = [];
}

public sealed class TrainingMetricCategoryAverageViewModel
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public double Rating { get; set; }
}
