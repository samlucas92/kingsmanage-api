using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class TrainingDevelopmentService : ITrainingDevelopmentService
{
	private static readonly IReadOnlyList<TrainingMetricDefinition> OutfieldMetrics =
	[
		Metric("passing", "Passing", "short-passing", "Short passing", "long-passing", "Long passing", "first-touch", "First touch", "decision-making", "Decision making"),
		Metric("shooting", "Shooting", "finishing", "Finishing", "shot-technique", "Shot technique", "composure", "Composure", "movement-to-shoot", "Movement to shoot"),
		Metric("defending", "Defending", "tackling", "Tackling", "pressing", "Pressing", "marking", "Marking", "recovery-runs", "Recovery runs"),
		Metric("positioning", "Positioning", "shape-discipline", "Shape discipline", "spacing", "Spacing", "transitions", "Transitions", "off-ball-movement", "Off-ball movement"),
		Metric("awareness", "Awareness", "scanning", "Scanning", "anticipation", "Anticipation", "risk-management", "Risk management", "game-understanding", "Game understanding"),
		Metric("teamwork", "Teamwork", "support-play", "Support play", "unselfishness", "Unselfishness", "pressing-triggers", "Pressing triggers", "role-discipline", "Role discipline"),
		Metric("fitness", "Fitness", "stamina", "Stamina", "speed", "Speed", "strength", "Strength", "agility", "Agility"),
		Metric("communication", "Communication", "calling-information", "Calling information", "organisation", "Organisation", "encouragement", "Encouragement", "listening", "Listening")
	];

	private static readonly IReadOnlyList<TrainingMetricDefinition> GoalkeeperMetrics =
	[
		Metric("handling", "Handling", "catching", "Catching", "parrying", "Parrying", "low-balls", "Low balls", "high-balls", "High balls"),
		Metric("shot-stopping", "Shot stopping", "reflexes", "Reflexes", "positioning", "Set positioning", "one-v-one", "1v1s", "recovery-saves", "Recovery saves"),
		Metric("box-command", "Box command", "cross-claiming", "Cross claiming", "starting-position", "Starting position", "defensive-organisation", "Defensive organisation", "bravery", "Bravery"),
		Metric("distribution", "Distribution", "short-distribution", "Short distribution", "long-distribution", "Long distribution", "throwing", "Throwing", "decision-making", "Decision making")
	];

	private readonly IMongoCollection<TrainingAssessment> assessments;
	private readonly TenantMongoScope tenant;

	static TrainingDevelopmentService()
	{
		if (!BsonClassMap.IsClassMapRegistered(typeof(TrainingAssessment)))
		{
			BsonClassMap.RegisterClassMap<TrainingAssessment>(classMap =>
			{
				classMap.AutoMap();
				classMap.SetIgnoreExtraElements(true);
			});
		}
	}

	public TrainingDevelopmentService(MongoContext context, TenantMongoScope tenant)
	{
		assessments = context.Database.GetCollection<TrainingAssessment>("trainingAssessments");
		this.tenant = tenant;
	}

	public IReadOnlyList<TrainingMetricDefinition> GetMetricDefinitions(TrainingPlayerRole playerRole)
	{
		return playerRole == TrainingPlayerRole.Goalkeeper ? GoalkeeperMetrics : OutfieldMetrics;
	}

	public async Task<IReadOnlyList<TrainingAssessment>> GetEventAssessmentsAsync(
		Guid eventId,
		CancellationToken cancellationToken = default)
	{
		var eventAssessments = await assessments
			.Find(tenant.Filter<TrainingAssessment>(assessment => assessment.EventId == eventId))
			.SortBy(assessment => assessment.PlayerId)
			.ToListAsync(cancellationToken);

		return eventAssessments.Select(NormaliseFromStorage).ToList();
	}

	public async Task<TrainingAssessment?> GetPlayerEventAssessmentAsync(
		Guid eventId,
		Guid playerId,
		CancellationToken cancellationToken = default)
	{
		var assessment = await assessments
			.Find(tenant.Filter<TrainingAssessment>(item => item.EventId == eventId && item.PlayerId == playerId))
			.FirstOrDefaultAsync(cancellationToken);

		return assessment is null ? null : NormaliseFromStorage(assessment);
	}

	public async Task<IReadOnlyList<TrainingAssessment>> GetPlayerAssessmentsAsync(
		Guid playerId,
		DateTime? from,
		DateTime? to,
		CancellationToken cancellationToken = default)
	{
		var filter = tenant.Filter<TrainingAssessment>(assessment => assessment.PlayerId == playerId);

		if (from is not null)
		{
			filter &= Builders<TrainingAssessment>.Filter.Gte(assessment => assessment.AssessedAt, from.Value);
		}

		if (to is not null)
		{
			filter &= Builders<TrainingAssessment>.Filter.Lte(assessment => assessment.AssessedAt, to.Value);
		}

		var playerAssessments = await assessments
			.Find(filter)
			.SortByDescending(assessment => assessment.AssessedAt)
			.ToListAsync(cancellationToken);

		return playerAssessments.Select(NormaliseFromStorage).ToList();
	}

	public async Task<TrainingAssessment> UpsertAsync(
		TrainingAssessment assessment,
		CancellationToken cancellationToken = default)
	{
		var existingAssessment = await GetPlayerEventAssessmentAsync(
			assessment.EventId,
			assessment.PlayerId,
			cancellationToken);

		assessment.Id = existingAssessment?.Id ?? (assessment.Id == Guid.Empty ? Guid.NewGuid() : assessment.Id);
		assessment.CreatedAt = existingAssessment?.CreatedAt ?? DateTime.UtcNow;
		assessment.UpdatedAt = DateTime.UtcNow;
		assessment.AssessedAt = DateTime.UtcNow;
		assessment.Notes = assessment.Notes.Trim();
		assessment.Metrics = NormaliseMetricRatings(assessment.Metrics, GetMetricDefinitions(assessment.PlayerRole));
		tenant.Assign(assessment);

		await assessments.ReplaceOneAsync(
			tenant.Filter<TrainingAssessment>(item => item.EventId == assessment.EventId && item.PlayerId == assessment.PlayerId),
			assessment,
			new ReplaceOptions { IsUpsert = true },
			cancellationToken);

		return assessment;
	}

	public async Task DeleteEventAssessmentsAsync(
		Guid eventId,
		CancellationToken cancellationToken = default)
	{
		await assessments.DeleteManyAsync(
			tenant.Filter<TrainingAssessment>(assessment => assessment.EventId == eventId),
			cancellationToken);
	}

	private static TrainingAssessment NormaliseFromStorage(TrainingAssessment assessment)
	{
		assessment.Metrics ??= [];
		assessment.Notes ??= string.Empty;
		if (assessment.CreatedAt == default) assessment.CreatedAt = DateTime.UtcNow;
		if (assessment.UpdatedAt == default) assessment.UpdatedAt = assessment.CreatedAt;
		if (assessment.AssessedAt == default) assessment.AssessedAt = assessment.UpdatedAt;

		return assessment;
	}

	private static List<TrainingMetricRating> NormaliseMetricRatings(
		IReadOnlyList<TrainingMetricRating> ratings,
		IReadOnlyList<TrainingMetricDefinition> definitions)
	{
		return definitions.Select(definition =>
		{
			var rating = ratings.FirstOrDefault(item => item.Key == definition.Key);

			return new TrainingMetricRating
			{
				Key = definition.Key,
				Label = definition.Label,
				Rating = ClampRating(rating?.Rating > 0 ? rating.Rating : AverageCategoryRating(rating)),
				Categories = definition.Categories.Select(category =>
				{
					var categoryRating = rating?.Categories.FirstOrDefault(item => item.Key == category.Key);
					return new TrainingMetricCategoryRating
					{
						Key = category.Key,
						Label = category.Label,
						Rating = ClampRating(categoryRating?.Rating ?? rating?.Rating ?? 3)
					};
				}).ToList()
			};
		}).ToList();
	}

	private static int AverageCategoryRating(TrainingMetricRating? rating)
	{
		if (rating?.Categories.Count > 0)
		{
			return (int)Math.Round(rating.Categories.Average(category => category.Rating), MidpointRounding.AwayFromZero);
		}

		return 3;
	}

	private static int ClampRating(int rating)
	{
		return Math.Clamp(rating, 1, 5);
	}

	private static TrainingMetricDefinition Metric(string key, string label, params string[] categoryPairs)
	{
		return new TrainingMetricDefinition
		{
			Key = key,
			Label = label,
			Categories = categoryPairs
				.Chunk(2)
				.Select(pair => new TrainingMetricCategoryDefinition
				{
					Key = pair[0],
					Label = pair[1]
				})
				.ToList()
		};
	}
}
