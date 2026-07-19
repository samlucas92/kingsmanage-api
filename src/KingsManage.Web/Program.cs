using System.Text;
using System.Text.Json.Serialization;
using KingsManage;
using KingsManage.Mongo;
using KingsManage.Mongo.Services;
using KingsManage.Web.Models;
using KingsManage.Web.Realtime;
using KingsManage.Web.Security;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
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

var r2StorageSettings = builder.Configuration
	.GetSection("R2")
	.Get<R2StorageSettings>() ?? new R2StorageSettings();
var fileLifecycleSettings = builder.Configuration
	.GetSection("FileLifecycle")
	.Get<FileLifecycleSettings>() ?? new FileLifecycleSettings();
var billingSettings = builder.Configuration
	.GetSection("Billing")
	.Get<BillingSettings>() ?? new BillingSettings();

r2StorageSettings.AccountId = Environment.GetEnvironmentVariable("R2_ACCOUNT_ID") ?? r2StorageSettings.AccountId;
r2StorageSettings.AccessKeyId = Environment.GetEnvironmentVariable("R2_ACCESS_KEY_ID") ?? r2StorageSettings.AccessKeyId;
r2StorageSettings.SecretAccessKey = Environment.GetEnvironmentVariable("R2_SECRET_ACCESS_KEY") ?? r2StorageSettings.SecretAccessKey;
r2StorageSettings.BucketName = Environment.GetEnvironmentVariable("R2_BUCKET_NAME") ?? r2StorageSettings.BucketName;
r2StorageSettings.PublicBaseUrl = Environment.GetEnvironmentVariable("R2_PUBLIC_BASE_URL") ?? r2StorageSettings.PublicBaseUrl;

jwtSettings.Secret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? jwtSettings.Secret;

if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
{
	jwtSettings.Secret = "development-only-kingsmanage-jwt-secret-change-this-before-production";
}

builder.Services.AddSingleton(mongoDbSettings);
builder.Services.AddSingleton(r2StorageSettings);
builder.Services.AddSingleton(fileLifecycleSettings);
builder.Services.AddSingleton(billingSettings);

builder.Services.Configure<JwtSettings>(options =>
{
	options.Issuer = jwtSettings.Issuer;
	options.Audience = jwtSettings.Audience;
	options.Secret = jwtSettings.Secret;
	options.ExpiryMinutes = jwtSettings.ExpiryMinutes;
});

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<TenantDataMigrator>();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<ITeamAccessContext, HttpTeamAccessContext>();
builder.Services.AddScoped<TenantMongoScope>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<ISeasonService, SeasonService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IClubEventService, ClubEventService>();
builder.Services.AddScoped<IClubPostService, ClubPostService>();
builder.Services.AddScoped<IClubPostTemplateService, ClubPostTemplateService>();
builder.Services.AddScoped<IClubTeamService, ClubTeamService>();
builder.Services.AddScoped<IClubNotificationService, ClubNotificationService>();
builder.Services.AddScoped<IClubFileService, ClubFileService>();
builder.Services.AddScoped<IStoredFileObjectService, StoredFileObjectService>();
builder.Services.AddScoped<IFileLifecycleService, FileLifecycleService>();
builder.Services.AddScoped<IFileStorageService, R2FileStorageService>();
builder.Services.AddScoped<RichTextAssetService>();
builder.Services.AddScoped<IPlayerStatsQueryService, PlayerStatsQueryService>();
builder.Services.AddScoped<IReportsQueryService, ReportsQueryService>();
builder.Services.AddSingleton<IFileContentScanner, BasicFileContentScanner>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IOrganizationDashboardService, OrganizationDashboardService>();
builder.Services.AddScoped<ISportsClubService, SportsClubService>();
builder.Services.AddScoped<IUserMembershipService, UserMembershipService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

if (fileLifecycleSettings.Enabled)
{
	builder.Services.AddHostedService<FileLifecycleBackgroundService>();
}

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
		options.Events = new JwtBearerEvents
		{
			OnMessageReceived = context =>
			{
				var accessToken = context.Request.Query["access_token"];
				var path = context.HttpContext.Request.Path;

				if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hubs/club"))
				{
					context.Token = accessToken;
				}

				return Task.CompletedTask;
			}
		};
	});

builder.Services.AddAuthorization(options =>
{
	options.AddPolicy("SiteAdmin", policy => policy.RequireClaim(
		HttpTenantContext.PlatformAdminClaim,
		"true"));

	options.AddPolicy("OrganizationAdmin", policy => policy.RequireAssertion(context =>
		context.User.HasClaim(HttpTenantContext.PlatformAdminClaim, "true") ||
		context.User.HasClaim(HttpTenantContext.TenantRoleClaim, TenantRole.OrganizationAdmin.ToString())));

	options.AddPolicy("ClubAdmin", policy => policy.RequireAssertion(context =>
		context.User.HasClaim(HttpTenantContext.PlatformAdminClaim, "true") ||
		context.User.HasClaim(HttpTenantContext.TenantRoleClaim, TenantRole.OrganizationAdmin.ToString()) ||
		context.User.HasClaim(HttpTenantContext.TenantRoleClaim, TenantRole.ClubAdmin.ToString())));

	options.AddPolicy("TeamManagement", policy => policy.RequireAssertion(context =>
		context.User.HasClaim(HttpTenantContext.PlatformAdminClaim, "true") ||
		context.User.HasClaim(HttpTenantContext.TenantRoleClaim, TenantRole.OrganizationAdmin.ToString()) ||
		context.User.HasClaim(HttpTenantContext.TenantRoleClaim, TenantRole.ClubAdmin.ToString()) ||
		context.User.HasClaim(HttpTenantContext.TenantRoleClaim, TenantRole.TeamManager.ToString()) ||
		context.User.HasClaim(HttpTenantContext.TenantRoleClaim, TenantRole.Coach.ToString())));
});

builder.Services
	.AddControllers()
	.AddNewtonsoftJson(options =>
	{
		options.SerializerSettings.Converters.Add(new StringEnumConverter());
	});

builder.Services
	.AddSignalR()
	.AddJsonProtocol(options =>
	{
		options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
	});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
	{
		Name = "Authorization",
		Type = SecuritySchemeType.Http,
		Scheme = JwtBearerDefaults.AuthenticationScheme,
		BearerFormat = "JWT",
		In = ParameterLocation.Header,
		Description = "JWT Authorization header using the Bearer scheme."
	});

	options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
	{
		[new OpenApiSecuritySchemeReference("bearer", document)] = []
	});
});

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
			.AllowAnyMethod()
			.AllowCredentials();
	});
});

var app = builder.Build();

if (builder.Configuration.GetValue("Tenancy:RunStartupMigration", true))
{
	await app.Services.GetRequiredService<TenantDataMigrator>().RunAsync();
}

await EnsureDefaultAdminUserAsync(app);

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Frontend");

app.UseAuthentication();
app.Use(async (context, next) =>
{
	try
	{
		await next();
	}
	catch (UnauthorizedAccessException exception)
	{
		context.Response.StatusCode = StatusCodes.Status403Forbidden;
		await context.Response.WriteAsJsonAsync(new { message = exception.Message });
	}
});
app.UseAuthorization();

app.MapControllers();
app.MapHub<ClubHub>("/hubs/club");

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
