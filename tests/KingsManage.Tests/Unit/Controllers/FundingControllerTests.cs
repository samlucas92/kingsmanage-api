using KingsManage;
using KingsManage.Web.Controllers;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public class FundingControllerTests
{
	[Test]
	public async Task BulkImport_WithValidRows_ShouldCreateEveryOpportunity()
	{
		var service = new FakeFundingOpportunityService();
		var controller = new FundingController(service);
		var model = new BulkFundingImportModel
		{
			Opportunities =
			[
				new SaveFundingOpportunityModel
				{
					Name = "Equipment Fund",
					Amount = "Up to £25,000",
					ApplicationUrl = "https://example.org/equipment",
					Status = FundingOpportunityStatus.Open,
					StatusDetail = "Open"
				},
				new SaveFundingOpportunityModel
				{
					Name = "Facilities Fund",
					ApplicationUrl = "https://example.org/facilities",
					Status = FundingOpportunityStatus.Closed,
					StatusDetail = "Closed / Track reopening"
				}
			]
		};

		var result = await controller.BulkImport(model, CancellationToken.None);
		var okResult = result.Result as OkObjectResult;
		var importResult = okResult?.Value as BulkFundingImportResult;

		Assert.That(importResult?.OpportunityCount, Is.EqualTo(2));
		Assert.That(service.Created, Has.Count.EqualTo(2));
		Assert.That(service.Created[1].StatusDetail, Is.EqualTo("Closed / Track reopening"));
		Assert.That(service.Created.All(item => item.ApplicationState == FundingApplicationState.Investigating), Is.True);
	}

	[Test]
	public async Task BulkImport_WithMoreThanMaximumRows_ShouldReturnBadRequest()
	{
		var controller = new FundingController(new FakeFundingOpportunityService());
		var model = new BulkFundingImportModel
		{
			Opportunities = Enumerable.Range(0, 251)
				.Select(index => new SaveFundingOpportunityModel { Name = $"Opportunity {index}" })
				.ToList()
		};

		var result = await controller.BulkImport(model, CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Create_WhenServiceFindsDuplicate_ShouldReturnConflict()
	{
		var service = new FakeFundingOpportunityService { RejectCreatesAsDuplicates = true };
		var controller = new FundingController(service);

		var result = await controller.Create(
			new SaveFundingOpportunityModel { Name = "Equipment Fund" },
			CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<ConflictObjectResult>());
	}

	[Test]
	public async Task Create_WithInvalidProgressValue_ShouldReturnBadRequest()
	{
		var controller = new FundingController(new FakeFundingOpportunityService());

		var result = await controller.Create(
			new SaveFundingOpportunityModel
			{
				Name = "Equipment Fund",
				ApplicationState = (FundingApplicationState)999
			},
			CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	private sealed class FakeFundingOpportunityService : IFundingOpportunityService
	{
		public List<FundingOpportunity> Created { get; } = [];
		public bool RejectCreatesAsDuplicates { get; init; }

		public Task<IReadOnlyList<FundingOpportunity>> GetAllAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<FundingOpportunity>>(Created);

		public Task<FundingOpportunity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult(Created.FirstOrDefault(item => item.Id == id));

		public Task<FundingOpportunity> CreateAsync(FundingOpportunity opportunity, CancellationToken cancellationToken = default)
		{
			if (RejectCreatesAsDuplicates) throw new ArgumentException("This funding opportunity already exists.");
			opportunity.Id = Guid.NewGuid();
			Created.Add(opportunity);
			return Task.FromResult(opportunity);
		}

		public Task<IReadOnlyList<FundingOpportunity>> CreateManyAsync(
			IEnumerable<FundingOpportunity> opportunities,
			CancellationToken cancellationToken = default)
		{
			Created.AddRange(opportunities);
			return Task.FromResult<IReadOnlyList<FundingOpportunity>>(Created);
		}

		public Task<FundingOpportunity?> UpdateAsync(FundingOpportunity opportunity, CancellationToken cancellationToken = default) =>
			Task.FromResult<FundingOpportunity?>(opportunity);

		public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(true);
	}
}
