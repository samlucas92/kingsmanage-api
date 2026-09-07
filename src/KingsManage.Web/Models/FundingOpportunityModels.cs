using KingsManage;

namespace KingsManage.Web.Models;

public sealed class SaveFundingOpportunityModel
{
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Amount { get; set; } = string.Empty;
	public string ApplicationUrl { get; set; } = string.Empty;
	public DateTime? StartDate { get; set; }
	public DateTime? EndDate { get; set; }
	public FundingOpportunityStatus Status { get; set; } = FundingOpportunityStatus.Open;
	public string StatusDetail { get; set; } = string.Empty;
	public FundingApplicationState ApplicationState { get; set; } = FundingApplicationState.Investigating;
	public string Notes { get; set; } = string.Empty;

	public FundingOpportunity ToOpportunity(Guid id = default) => new()
	{
		Id = id,
		Name = Name,
		Description = Description,
		Amount = Amount,
		ApplicationUrl = ApplicationUrl,
		StartDate = StartDate,
		EndDate = EndDate,
		Status = Status,
		StatusDetail = StatusDetail,
		ApplicationState = ApplicationState,
		Notes = Notes
	};
}

public sealed class BulkFundingImportModel
{
	public List<SaveFundingOpportunityModel> Opportunities { get; set; } = [];
}

public sealed record BulkFundingImportResult(int OpportunityCount);
