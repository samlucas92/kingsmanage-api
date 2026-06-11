using KingsManage;
using KingsManage.Mongo;
using MongoPlayerService = KingsManage.Mongo.Services.PlayerService;
using MongoSeasonService = KingsManage.Mongo.Services.SeasonService;
using MongoMatchService = KingsManage.Mongo.Services.MatchService;
using Newtonsoft.Json.Converters;

var builder = WebApplication.CreateBuilder(args);

var mongoDbSettings = builder.Configuration
	.GetSection("MongoDb")
	.Get<MongoDbSettings>();

if (mongoDbSettings is null)
{
	throw new InvalidOperationException("MongoDB settings are missing.");
}

builder.Services.AddSingleton(mongoDbSettings);
builder.Services.AddSingleton<MongoContext>();

builder.Services.AddScoped<IPlayerService, MongoPlayerService>();
builder.Services.AddScoped<ISeasonService, MongoSeasonService>();
builder.Services.AddScoped<IMatchService, MongoMatchService>();

builder.Services
	.AddControllers()
	.AddNewtonsoftJson(options =>
	{
		options.SerializerSettings.Converters.Add(new StringEnumConverter());
	});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
	options.AddPolicy("Frontend", policy =>
	{
		policy
			.WithOrigins(
				"http://localhost:5173",
				"https://localhost:5173"
			)
			.AllowAnyHeader()
			.AllowAnyMethod();
	});
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseCors("Frontend");

app.MapControllers();

app.MapGet("/api/health", () =>
{
	return Results.Ok(new
	{
		status = "Healthy",
		service = "KingsManage.Web",
		timestamp = DateTime.UtcNow
	});
});

app.Run();

public partial class Program;