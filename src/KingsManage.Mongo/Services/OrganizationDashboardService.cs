using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class OrganizationDashboardService : IOrganizationDashboardService
{
	private readonly IMongoCollection<SportsClub> _clubs;
	private readonly IMongoCollection<ClubTeamProfile> _teams;
	private readonly IMongoCollection<AppUser> _users;
	private readonly IMongoCollection<Player> _players;
	private readonly IMongoCollection<Match> _matches;
	private readonly IMongoCollection<ClubEvent> _events;
	private readonly IMongoCollection<FinanceTransaction> _transactions;
	private readonly ITenantContext _tenant;

	public OrganizationDashboardService(MongoContext context, ITenantContext tenant)
	{
		_clubs = context.Database.GetCollection<SportsClub>("clubs");
		_teams = context.Database.GetCollection<ClubTeamProfile>("clubTeamProfiles");
		_users = context.Database.GetCollection<AppUser>("users");
		_players = context.Database.GetCollection<Player>("players");
		_matches = context.Database.GetCollection<Match>("matches");
		_events = context.Database.GetCollection<ClubEvent>("events");
		_transactions = context.Database.GetCollection<FinanceTransaction>("financeTransactions");
		_tenant = tenant;
	}

	public async Task<OrganizationDashboard?> GetAsync(
		Guid? clubId = null,
		CancellationToken cancellationToken = default)
	{
		var clubs = await _clubs
			.Find(club => club.OrganizationId == _tenant.OrganizationId)
			.SortBy(club => club.Name)
			.ToListAsync(cancellationToken);
		if (clubId.HasValue && clubs.All(club => club.Id != clubId.Value)) return null;

		var selectedClubIds = clubs
			.Where(club => !clubId.HasValue || club.Id == clubId.Value)
			.Select(club => club.Id)
			.ToHashSet();
		var tenantFilter = Builders<ClubTeamProfile>.Filter.Eq(
			team => team.OrganizationId,
			_tenant.OrganizationId);
		var teams = await _teams.Find(tenantFilter).ToListAsync(cancellationToken);
		var players = await _players
			.Find(player =>
				player.OrganizationId == _tenant.OrganizationId &&
				selectedClubIds.Contains(player.ClubId))
			.ToListAsync(cancellationToken);
		var users = await _users
			.Find(user => user.Memberships.Any(membership =>
				membership.OrganizationId == _tenant.OrganizationId))
			.ToListAsync(cancellationToken);
		var matches = await _matches
			.Find(match =>
				match.OrganizationId == _tenant.OrganizationId &&
				selectedClubIds.Contains(match.ClubId) &&
				!match.IsCompleted &&
				match.Date >= DateTime.UtcNow)
			.SortBy(match => match.Date)
			.ToListAsync(cancellationToken);
		var events = await _events
			.Find(clubEvent =>
				clubEvent.OrganizationId == _tenant.OrganizationId &&
				selectedClubIds.Contains(clubEvent.ClubId) &&
				clubEvent.StartDateTime >= DateTime.UtcNow)
			.SortBy(clubEvent => clubEvent.StartDateTime)
			.ToListAsync(cancellationToken);
		var transactions = await _transactions
			.Find(transaction =>
				transaction.OrganizationId == _tenant.OrganizationId &&
				selectedClubIds.Contains(transaction.ClubId))
			.ToListAsync(cancellationToken);

		var clubNames = clubs.ToDictionary(club => club.Id, club => club.Name);
		var finance = BuildFinanceSummary(transactions);
		var clubSummaries = clubs
			.Where(club => selectedClubIds.Contains(club.Id))
			.Select(club => BuildClubSummary(club, teams, users, players, transactions, matches, events))
			.ToList();

		return new OrganizationDashboard
		{
			ClubCount = selectedClubIds.Count,
			TeamCount = teams.Count(team => selectedClubIds.Contains(team.ClubId) && team.IsActive),
			UserCount = users.Count(user => HasSelectedClubAccess(user, selectedClubIds)),
			PlayerCount = players.Count(player => player.IsActive),
			Finance = finance,
			Clubs = clubSummaries,
			UpcomingFixtures = matches.Take(10).Select(match => new OrganizationUpcomingItem
			{
				Id = match.Id,
				ClubId = match.ClubId,
				ClubName = clubNames.GetValueOrDefault(match.ClubId, "Unknown club"),
				Title = $"vs {match.Opponent}",
				StartsAt = match.Date,
				Location = match.Location
			}).ToList(),
			UpcomingEvents = events.Take(10).Select(clubEvent => new OrganizationUpcomingItem
			{
				Id = clubEvent.Id,
				ClubId = clubEvent.ClubId,
				ClubName = clubNames.GetValueOrDefault(clubEvent.ClubId, "Unknown club"),
				Title = clubEvent.Title,
				StartsAt = clubEvent.StartDateTime,
				Location = clubEvent.Location
			}).ToList()
		};
	}

	private OrganizationClubSummary BuildClubSummary(
		SportsClub club,
		IReadOnlyList<ClubTeamProfile> teams,
		IReadOnlyList<AppUser> users,
		IReadOnlyList<Player> players,
		IReadOnlyList<FinanceTransaction> transactions,
		IReadOnlyList<Match> matches,
		IReadOnlyList<ClubEvent> events)
	{
		var clubTeams = teams.Where(team => team.ClubId == club.Id && team.IsActive).ToList();
		var clubPlayers = players.Where(player => player.ClubId == club.Id && player.IsActive).ToList();
		var clubUsers = users.Where(user => user.Memberships.Any(membership =>
			membership.OrganizationId == _tenant.OrganizationId &&
			(membership.ClubId == club.Id || membership.ClubId == null))).ToList();
		var outstanding = BuildFinanceSummary(
			transactions.Where(transaction => transaction.ClubId == club.Id)).Outstanding;
		var attention = new List<string>();
		if (!club.IsActive) attention.Add("Club is archived");
		if (clubTeams.Count == 0) attention.Add("No active teams");
		if (clubPlayers.Count == 0) attention.Add("No active players");
		if (outstanding > 0) attention.Add("Outstanding finance requires attention");
		if (!matches.Any(match => match.ClubId == club.Id) &&
			!events.Any(clubEvent => clubEvent.ClubId == club.Id))
			attention.Add("No upcoming activity");

		return new OrganizationClubSummary
		{
			ClubId = club.Id,
			ClubName = club.Name,
			IsActive = club.IsActive,
			TeamCount = clubTeams.Count,
			UserCount = clubUsers.Count,
			PlayerCount = clubPlayers.Count,
			OutstandingFinance = outstanding,
			Attention = attention
		};
	}

	private static OrganizationFinanceSummary BuildFinanceSummary(
		IEnumerable<FinanceTransaction> transactions)
	{
		var list = transactions.ToList();
		var charged = list
			.Where(transaction => transaction.Type == FinanceTransactionType.Charge)
			.Sum(transaction => transaction.Amount);
		var paid = list
			.Where(transaction => transaction.Type == FinanceTransactionType.Payment)
			.Sum(transaction => transaction.Amount);
		var adjustments = list
			.Where(transaction => transaction.Type == FinanceTransactionType.Adjustment)
			.Sum(transaction => transaction.Amount);
		return new OrganizationFinanceSummary
		{
			Charged = charged,
			Paid = paid,
			Adjustments = adjustments,
			Outstanding = Math.Max(0, charged + adjustments - paid)
		};
	}

	private static bool HasSelectedClubAccess(AppUser user, HashSet<Guid> selectedClubIds) =>
		user.IsActive && user.Memberships.Any(membership =>
			membership.ClubId == null || selectedClubIds.Contains(membership.ClubId.Value));
}
