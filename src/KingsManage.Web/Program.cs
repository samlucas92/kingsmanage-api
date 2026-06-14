using System.Text;
using KingsManage;
using KingsManage.Mongo;
using KingsManage.Web.Models;
using KingsManage.Web.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoClubEventService = KingsManage.Mongo.Services.ClubEventService;
using MongoFinanceService = KingsManage.Mongo.Services.FinanceService;
using MongoMatchService = KingsManage.Mongo.Services.MatchService;
using MongoPlayerService = KingsManage.Mongo.Services.PlayerService;
using MongoSeasonService = KingsManage.Mongo.Services.SeasonService;
using MongoStatsService = KingsManage.Mongo.Services.StatsService;
using MongoUserService = KingsManage.Mongo.Services.UserService;
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

var jwtSettings = builder.Configuration
	.GetSection("Jwt")
	.Get<JwtSettings>() ?? new JwtSettings();

jwtSettings.Secret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? jwtSettings.Secret;

if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
{
	jwtSettings.Secret = "development-only-kingsmanage-jwt-secret-change-this-before-production";
}

builder.Services.AddSingleton(mongoDbSettings);

builder.Services.Configure<JwtSettings>(options =>
{
	options.Issuer = jwtSettings.Issuer;
	options.Audience = jwtSettings.Audience;
	options.Secret = jwtSettings.Secret;
	options.ExpiryMinutes = jwtSettings.ExpiryMinutes;
});

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<IPlayerService, MongoPlayerService>();
builder.Services.AddScoped<ISeasonService, MongoSeasonService>();
builder.Services.AddScoped<IMatchService, MongoMatchService>();
builder.Services.AddScoped<IStatsService, MongoStatsService>();
builder.Services.AddScoped<IFinanceService, MongoFinanceService>();
builder.Services.AddScoped<IUserService, MongoUserService>();
builder.Services.AddScoped<IClubEventService, MongoClubEventService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));

builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			ValidIssuer = jwtSettings.Issuer,
			ValidAudience = jwtSettings.Audience,
			IssuerSigningKey = signingKey,
			ClockSkew = TimeSpan.FromMinutes(2)
		};
	});

builder.Services.AddAuthorization();

builder.Services
	.AddControllers()
	.AddNewtonsoftJson(options =>
	{
		options.SerializerSettings.Converters.Add(new StringEnumConverter());
	});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedCorsOrigins = builder.Configuration
	.GetSection("AllowedCorsOrigins")
	.Get<string[]>() ?? [
		"http://localhost:5173",
		"https://localhost:5173"
	];

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

await EnsureDefaultAdminUserAsync(app);

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static async Task EnsureDefaultAdminUserAsync(WebApplication app)
{
	using var scope = app.Services.CreateScope();

	var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
	var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

	var defaultAdminSettings = configuration
		.GetSection("DefaultAdmin")
		.Get<DefaultAdminSettings>() ?? new DefaultAdminSettings();

	var defaultAdminEmail =
		Environment.GetEnvironmentVariable("DEFAULT_ADMIN_EMAIL") ??
		defaultAdminSettings.Email;

	var defaultAdminPassword =
		Environment.GetEnvironmentVariable("DEFAULT_ADMIN_PASSWORD") ??
		defaultAdminSettings.Password;

	if (string.IsNullOrWhiteSpace(defaultAdminEmail))
	{
		defaultAdminEmail = "admin@kingsmanage.local";
	}

	if (string.IsNullOrWhiteSpace(defaultAdminPassword))
	{
		defaultAdminPassword = "ChangeMe123!";
	}

	await userService.EnsureDefaultAdminUserAsync(
		defaultAdminEmail,
		defaultAdminPassword
	);
}

public partial class Program;
