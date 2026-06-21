using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class UserMembershipService : IUserMembershipService
{
	private readonly IMongoCollection<AppUser> _users;
	private readonly IMongoCollection<SportsClub> _clubs;
	private readonly IMongoCollection<ClubTeamProfile> _teams;
	private readonly ITenantContext _tenant;

	public UserMembershipService(MongoContext context, ITenantContext tenant)
	{
		_users = context.Database.GetCollection<AppUser>("users");
		_clubs = context.Database.GetCollection<SportsClub>("clubs");
		_teams = context.Database.GetCollection<ClubTeamProfile>("clubTeamProfiles");
		_tenant = tenant;
	}

	public async Task<IReadOnlyList<MembershipClubOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
	{
		var clubs = await _clubs.Find(club => club.OrganizationId == _tenant.OrganizationId && club.IsActive)
			.SortBy(club => club.Name).ToListAsync(cancellationToken);
		var clubIds = clubs.Select(club => club.Id).ToList();
		var teams = await _teams.Find(team => team.OrganizationId == _tenant.OrganizationId && clubIds.Contains(team.ClubId) && team.IsActive)
			.SortBy(team => team.SortOrder).ThenBy(team => team.DisplayName).ToListAsync(cancellationToken);

		return clubs.Select(club => new MembershipClubOption
		{
			Id = club.Id,
			Name = club.Name,
			Teams = teams.Where(team => team.ClubId == club.Id)
				.Select(team => new MembershipTeamOption { Id = team.Id, Name = team.DisplayName }).ToList()
		}).ToList();
	}

	public async Task<AppUser?> UpdateAsync(
		Guid userId,
		IReadOnlyList<UserMembership> memberships,
		Guid? defaultClubId,
		CancellationToken cancellationToken = default)
	{
		var user = await _users.Find(existing => existing.Id == userId && existing.Memberships.Any(membership => membership.OrganizationId == _tenant.OrganizationId))
			.FirstOrDefaultAsync(cancellationToken);
		if (user is null) return null;

		var normalised = memberships.Select(membership => new UserMembership
		{
			OrganizationId = _tenant.OrganizationId,
			ClubId = membership.ClubId,
			TeamId = membership.TeamId,
			Role = membership.Role
		}).ToList();
		await ValidateAsync(normalised, defaultClubId, cancellationToken);

		var wasOrganizationAdmin = user.Memberships.Any(membership =>
			membership.OrganizationId == _tenant.OrganizationId && membership.Role == TenantRole.OrganizationAdmin);
		var remainsOrganizationAdmin = normalised.Any(membership => membership.Role == TenantRole.OrganizationAdmin);
		if (wasOrganizationAdmin && !remainsOrganizationAdmin)
		{
			var adminCount = await _users.CountDocumentsAsync(existing => existing.IsActive && existing.Memberships.Any(membership =>
				membership.OrganizationId == _tenant.OrganizationId && membership.Role == TenantRole.OrganizationAdmin), cancellationToken: cancellationToken);
			if (adminCount <= 1) throw new InvalidOperationException("The final Organization Admin cannot be removed.");
		}

		var otherOrganizations = user.Memberships.Where(membership => membership.OrganizationId != _tenant.OrganizationId);
		user.Memberships = otherOrganizations.Concat(normalised).ToList();
		user.DefaultOrganizationId = _tenant.OrganizationId;
		user.DefaultClubId = defaultClubId ?? normalised.Select(membership => membership.ClubId).FirstOrDefault(id => id.HasValue) ?? _tenant.ClubId;
		user.Role = MapLegacyRole(normalised);
		user.UpdatedAt = DateTime.UtcNow;

		await _users.ReplaceOneAsync(existing => existing.Id == user.Id, user, cancellationToken: cancellationToken);
		return user;
	}

	private async Task ValidateAsync(List<UserMembership> memberships, Guid? defaultClubId, CancellationToken cancellationToken)
	{
		if (memberships.Count == 0) throw new ArgumentException("At least one membership is required.");
		if (memberships.GroupBy(membership => new { membership.ClubId, membership.TeamId, membership.Role }).Any(group => group.Count() > 1))
			throw new ArgumentException("Duplicate memberships are not allowed.");

		var clubs = await _clubs.Find(club => club.OrganizationId == _tenant.OrganizationId && club.IsActive).ToListAsync(cancellationToken);
		var clubIds = clubs.Select(club => club.Id).ToHashSet();
		var teams = await _teams.Find(team => team.OrganizationId == _tenant.OrganizationId && team.IsActive).ToListAsync(cancellationToken);

		foreach (var membership in memberships)
		{
			if (membership.Role == TenantRole.OrganizationAdmin)
			{
				if (membership.ClubId.HasValue || membership.TeamId.HasValue) throw new ArgumentException("Organization Admin access cannot be limited to a club or team.");
				continue;
			}
			if (!membership.ClubId.HasValue || !clubIds.Contains(membership.ClubId.Value)) throw new ArgumentException("A valid club is required for this role.");
			if (membership.Role == TenantRole.ClubAdmin && membership.TeamId.HasValue) throw new ArgumentException("Club Admin access cannot be limited to a team.");
			if (membership.Role == TenantRole.TeamManager && !membership.TeamId.HasValue) throw new ArgumentException("A team is required for Team Manager access.");
			if (membership.TeamId.HasValue && !teams.Any(team => team.Id == membership.TeamId && team.ClubId == membership.ClubId))
				throw new ArgumentException("The selected team does not belong to the selected club.");
		}

		if (defaultClubId.HasValue && !clubIds.Contains(defaultClubId.Value)) throw new ArgumentException("The default club is invalid.");
		var hasOrganizationAccess = memberships.Any(membership => membership.Role == TenantRole.OrganizationAdmin);
		if (defaultClubId.HasValue && !hasOrganizationAccess && !memberships.Any(membership => membership.ClubId == defaultClubId))
			throw new ArgumentException("The default club must be one of the user's memberships.");
	}

	private static UserRole MapLegacyRole(IEnumerable<UserMembership> memberships)
	{
		if (memberships.Any(membership => membership.Role is TenantRole.OrganizationAdmin or TenantRole.ClubAdmin)) return UserRole.Admin;
		if (memberships.Any(membership => membership.Role is TenantRole.TeamManager or TenantRole.Coach)) return UserRole.Coach;
		return UserRole.Player;
	}
}
