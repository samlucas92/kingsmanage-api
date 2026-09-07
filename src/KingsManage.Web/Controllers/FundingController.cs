using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "ClubAdmin")]
[Route("api/funding")]
public sealed class FundingController : ControllerBase
{
	private const int MaximumImportRows = 250;
	private readonly IFundingOpportunityService fundingService;

	public FundingController(IFundingOpportunityService fundingService)
	{
		this.fundingService = fundingService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<FundingOpportunity>>> GetAll(CancellationToken cancellationToken)
	{
		return Ok(await fundingService.GetAllAsync(cancellationToken));
	}

	[HttpGet("{id:guid}")]
	public async Task<ActionResult<FundingOpportunity>> GetById(Guid id, CancellationToken cancellationToken)
	{
		var opportunity = await fundingService.GetByIdAsync(id, cancellationToken);
		return opportunity is null ? NotFound() : Ok(opportunity);
	}

	[HttpPost]
	public async Task<ActionResult<FundingOpportunity>> Create(
		SaveFundingOpportunityModel model,
		CancellationToken cancellationToken)
	{
		var validationError = Validate(model);
		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		try
		{
			var created = await fundingService.CreateAsync(model.ToOpportunity(), cancellationToken);
			return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
		}
		catch (ArgumentException exception)
		{
			return Conflict(exception.Message);
		}
	}

	[HttpPut("{id:guid}")]
	public async Task<ActionResult<FundingOpportunity>> Update(
		Guid id,
		SaveFundingOpportunityModel model,
		CancellationToken cancellationToken)
	{
		var validationError = Validate(model);
		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		try
		{
			var updated = await fundingService.UpdateAsync(model.ToOpportunity(id), cancellationToken);
			return updated is null ? NotFound() : Ok(updated);
		}
		catch (ArgumentException exception)
		{
			return Conflict(exception.Message);
		}
	}

	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
	{
		return await fundingService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
	}

	[HttpPost("bulk")]
	public async Task<ActionResult<BulkFundingImportResult>> BulkImport(
		BulkFundingImportModel model,
		CancellationToken cancellationToken)
	{
		if (model.Opportunities is null || model.Opportunities.Count == 0)
		{
			return BadRequest("Add at least one funding opportunity to import.");
		}

		if (model.Opportunities.Count > MaximumImportRows)
		{
			return BadRequest($"Import up to {MaximumImportRows} funding opportunities at a time.");
		}

		for (var index = 0; index < model.Opportunities.Count; index++)
		{
			var error = Validate(model.Opportunities[index]);
			if (error is not null)
			{
				return BadRequest($"Row {index + 2}: {error}");
			}
		}

		try
		{
			var created = await fundingService.CreateManyAsync(
				model.Opportunities.Select(opportunity => opportunity.ToOpportunity()),
				cancellationToken);
			return Ok(new BulkFundingImportResult(created.Count));
		}
		catch (ArgumentException exception)
		{
			return Conflict(exception.Message);
		}
	}

	private static string? Validate(SaveFundingOpportunityModel model)
	{
		if (string.IsNullOrWhiteSpace(model.Name)) return "Opportunity name is required.";
		if (model.Name.Trim().Length > 200) return "Opportunity name must be 200 characters or fewer.";
		if ((model.Description ?? string.Empty).Trim().Length > 2_000) return "Description must be 2,000 characters or fewer.";
		if ((model.Amount ?? string.Empty).Trim().Length > 100) return "Amount must be 100 characters or fewer.";
		if ((model.StatusDetail ?? string.Empty).Trim().Length > 200) return "Status detail must be 200 characters or fewer.";
		if ((model.Notes ?? string.Empty).Trim().Length > 5_000) return "Notes must be 5,000 characters or fewer.";
		if (!Enum.IsDefined(model.Status)) return "Opportunity status is invalid.";
		if (!Enum.IsDefined(model.ApplicationState)) return "Club progress is invalid.";

		if (!string.IsNullOrWhiteSpace(model.ApplicationUrl) &&
			(!Uri.TryCreate(model.ApplicationUrl.Trim(), UriKind.Absolute, out var uri) ||
				(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
		{
			return "Application link must be a valid http or https URL.";
		}

		if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate.Value.Date < model.StartDate.Value.Date)
		{
			return "End date cannot be before the start date.";
		}

		return null;
	}
}
