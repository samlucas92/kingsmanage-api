using KingsManage;
using KingsManage.Mongo;
using MongoPlayerService = KingsManage.Mongo.Services.PlayerService;
using MongoSeasonService = KingsManage.Mongo.Services.SeasonService;
using MongoMatchService = KingsManage.Mongo.Services.MatchService;
using Newtonsoft.Json.Converters;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(port))
{
	builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var mongoDbSettings = builder.Configuration
	.GetSection("MongoDb")
	.Get<MongoDbSettings>();

if (mongoDbSettings is null)
{
	throw new InvalidOperationException("MongoDB settings are missing.");
}

var allowedCorsOrigins = builder.Configuration
	.GetSection("AllowedCorsOrigins")
	.Get<string[]>() ?? [];

if (allowedCorsOrigins.Length == 0)
{
	allowedCorsOrigins =
	[
		"http://localhost:5173",
		"https://localhost:5173"
	];
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
			.WithOrigins(allowedCorsOrigins)
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

if (!app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

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