using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class PlatformOrganizationOnboardingService : IPlatformOrganizationOnboardingService
{
	private readonly IMongoCollection<Organization> organizations;
	private readonly IMongoCollection<SportsClub> clubs;
	private readonly IMongoCollection<AppUser> users;
	private readonly IMongoCollection<OrganizationSubscription> subscriptions;
	private readonly BillingSettings billingSettings;

	public PlatformOrganizationOnboardingService(
		MongoContext context,
		BillingSettings billingSettings)
	{
		organizations = context.Database.GetCollection<Organization>("organizations");
		clubs = context.Database.GetCollection<SportsClub>("clubs");
		users = context.Database.GetCollection<AppUser>("users");
		subscriptions = context.Database.GetCollection<OrganizationSubscription>("organizationSubscriptions");
		this.billingSettings = billingSettings;
	}

	public async Task<PlatformOrganizationOnboardingOutcome> CreateAsync(
		PlatformOrganizationOnboardingInput input,
		CancellationToken cancellationToken = default)
	{
		Normalise(input);
		if (await organizations.Find(item => item.Slug == input.OrganizationSlug).AnyAsync(cancellationToken))
			return new(PlatformOrganizationOnboardingStatus.OrganizationSlugExists);
		if (await users.Find(item => item.Email == input.AdministratorEmail).AnyAsync(cancellationToken))
			return new(PlatformOrganizationOnboardingStatus.AdministratorEmailExists);

		var now = DateTime.UtcNow;
		var organization = new Organization
		{
			Id = Guid.NewGuid(),
			Name = input.OrganizationName,
			Slug = input.OrganizationSlug,
			IsActive = true,
			CreatedAt = now,
			UpdatedAt = now
		};
		if (await clubs.Find(item =>
			item.OrganizationId == organization.Id && item.Slug == input.ClubSlug)
			.AnyAsync(cancellationToken))
			return new(PlatformOrganizationOnboardingStatus.ClubSlugExists);

		var club = new SportsClub
		{
			Id = Guid.NewGuid(),
			OrganizationId = organization.Id,
			Name = input.ClubName,
			Slug = input.ClubSlug,
			SportKey = input.SportKey,
			PrimaryColor = input.PrimaryColor,
			SecondaryColor = input.SecondaryColor,
			ContactEmail = input.ClubContactEmail,
			IsActive = true,
			CreatedAt = now,
			UpdatedAt = now
		};
		var administrator = new AppUser
		{
			Id = Guid.NewGuid(),
			Email = input.AdministratorEmail,
			PasswordHash = UserService.HashPassword(input.TemporaryPassword),
			Role = UserRole.Admin,
			DefaultOrganizationId = organization.Id,
			DefaultClubId = club.Id,
			Memberships =
			[
				new UserMembership
				{
					OrganizationId = organization.Id,
					ClubId = null,
					TeamId = null,
					Role = TenantRole.OrganizationAdmin
				}
			],
			IsActive = true,
			CreatedAt = now,
			UpdatedAt = now
		};
		var subscription = new OrganizationSubscription
		{
			Id = Guid.NewGuid(),
			OrganizationId = organization.Id,
			Status = input.SubscriptionStatus,
			ClubAllowance = input.ClubAllowance,
			BaseMonthlyPrice = billingSettings.BaseMonthlyPrice,
			AdditionalClubMonthlyPrice = billingSettings.AdditionalClubMonthlyPrice,
			Currency = billingSettings.Currency,
			BillingEmail = input.BillingEmail,
			TrialEndsAt = input.SubscriptionStatus == SubscriptionStatus.Trialing
				? now.AddDays(Math.Max(1, billingSettings.TrialDays))
				: null,
			CreatedAt = now,
			UpdatedAt = now
		};

		var organizationCreated = false;
		var clubCreated = false;
		var administratorCreated = false;
		try
		{
			await organizations.InsertOneAsync(organization, cancellationToken: cancellationToken);
			organizationCreated = true;
			await clubs.InsertOneAsync(club, cancellationToken: cancellationToken);
			clubCreated = true;
			await users.InsertOneAsync(administrator, cancellationToken: cancellationToken);
			administratorCreated = true;
			await subscriptions.InsertOneAsync(subscription, cancellationToken: cancellationToken);
		}
		catch (MongoWriteException exception) when (
			exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			await RollBackAsync(
				organization,
				club,
				administrator,
				organizationCreated,
				clubCreated,
				administratorCreated,
				cancellationToken);
			if (!organizationCreated)
				return new(PlatformOrganizationOnboardingStatus.OrganizationSlugExists);
			if (!clubCreated)
				return new(PlatformOrganizationOnboardingStatus.ClubSlugExists);
			if (!administratorCreated)
				return new(PlatformOrganizationOnboardingStatus.AdministratorEmailExists);
			throw;
		}
		catch
		{
			await RollBackAsync(
				organization,
				club,
				administrator,
				organizationCreated,
				clubCreated,
				administratorCreated,
				cancellationToken);
			throw;
		}

		return new(
			PlatformOrganizationOnboardingStatus.Created,
			new PlatformOrganizationOnboardingResult
			{
				Organization = organization,
				Club = club,
				AdministratorEmail = administrator.Email,
				Subscription = subscription
			});
	}

	private async Task RollBackAsync(
		Organization organization,
		SportsClub club,
		AppUser administrator,
		bool organizationCreated,
		bool clubCreated,
		bool administratorCreated,
		CancellationToken cancellationToken)
	{
		await subscriptions.DeleteOneAsync(
			item => item.OrganizationId == organization.Id,
			cancellationToken);
		if (administratorCreated)
			await users.DeleteOneAsync(item => item.Id == administrator.Id, cancellationToken);
		if (clubCreated)
			await clubs.DeleteOneAsync(item => item.Id == club.Id, cancellationToken);
		if (organizationCreated)
			await organizations.DeleteOneAsync(item => item.Id == organization.Id, cancellationToken);
	}

	private static void Normalise(PlatformOrganizationOnboardingInput input)
	{
		input.OrganizationName = input.OrganizationName.Trim();
		input.OrganizationSlug = input.OrganizationSlug.Trim().ToLowerInvariant();
		input.ClubName = input.ClubName.Trim();
		input.ClubSlug = input.ClubSlug.Trim().ToLowerInvariant();
		input.SportKey = input.SportKey.Trim().ToLowerInvariant();
		input.PrimaryColor = input.PrimaryColor.Trim().ToLowerInvariant();
		input.SecondaryColor = input.SecondaryColor.Trim().ToLowerInvariant();
		input.ClubContactEmail = input.ClubContactEmail.Trim().ToLowerInvariant();
		input.AdministratorEmail = input.AdministratorEmail.Trim().ToLowerInvariant();
		input.BillingEmail = input.BillingEmail.Trim().ToLowerInvariant();
	}
}
