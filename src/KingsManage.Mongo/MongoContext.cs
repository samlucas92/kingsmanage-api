using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace KingsManage.Mongo;

public class MongoContext
{
	private static readonly object MongoConfigurationLock = new();
	private static bool isMongoConfigured;

	public IMongoDatabase Database { get; }

	public MongoContext(MongoDbSettings settings)
	{
		ConfigureMongoSerialization();

		var client = new MongoClient(settings.ConnectionString);
		Database = client.GetDatabase(settings.DatabaseName);
	}

	private static void ConfigureMongoSerialization()
	{
		if (isMongoConfigured)
		{
			return;
		}

		lock (MongoConfigurationLock)
		{
			if (isMongoConfigured)
			{
				return;
			}

			BsonSerializer.RegisterSerializer(
				new GuidSerializer(GuidRepresentation.Standard)
			);

			isMongoConfigured = true;
		}
	}
}
