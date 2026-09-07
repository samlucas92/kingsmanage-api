namespace KingsManage;

public interface IFundingOpportunityService
{
	Task<IReadOnlyList<FundingOpportunity>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<FundingOpportunity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<FundingOpportunity> CreateAsync(FundingOpportunity opportunity, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<FundingOpportunity>> CreateManyAsync(IEnumerable<FundingOpportunity> opportunities, CancellationToken cancellationToken = default);
	Task<FundingOpportunity?> UpdateAsync(FundingOpportunity opportunity, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
