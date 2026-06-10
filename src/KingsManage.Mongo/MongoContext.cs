using MongoDB.Driver;

namespace KingsManage.Mongo;

public class MongoContext
{
	private readonly IMongoDatabase _database;

	public MongoContext(MongoDbSettings settings)
	{
		if (string.IsNullOrWhiteSpace(settings.ConnectionString))
		{
			throw new InvalidOperationException("MongoDB connection string is missing.");
		}

		if (string.IsNullOrWhiteSpace(settings.DatabaseName))
		{
			throw new InvalidOperationException("MongoDB database name is missing.");
		}

		var client = new MongoClient(settings.ConnectionString);

		_database = client.GetDatabase(settings.DatabaseName);
	}

	public IMongoDatabase Database => _database;
}