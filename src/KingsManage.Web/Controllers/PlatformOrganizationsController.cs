using KingsManage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "SiteAdmin")]
[Route("api/platform/organizations")]
public sealed class PlatformOrganizationsController : ControllerBase
{
	private readonly IOrganizationService organizations;
	private readonly IPlatformOrganizationOnboardingService onboarding;

	public PlatformOrganizationsController(
		IOrganizationService organizations,
		IPlatformOrganizationOnboardingService onboarding)
	{
		this.organizations = organizations;
		this.onboarding = onboarding;
	}

	[HttpPost("onboard")]
	public async Task<ActionResult<PlatformOrganizationOnboardingResult>> Onboard(
		PlatformOrganizationOnboardingInput input,
		CancellationToken cancellationToken)
	{
		var error = ValidateOnboarding(input);
		if (error is not null) return BadRequest(error);

		var outcome = await onboarding.CreateAsync(input, cancellationToken);
		return outcome.Status switch
		{
			PlatformOrganizationOnboardingStatus.Created => Created(
				$"/api/platform/organizations/{outcome.Result!.Organization.Id}",
				outcome.Result),
			PlatformOrganizationOnboardingStatus.OrganizationSlugExists => Conflict(
				"An organization with this slug already exists."),
			PlatformOrganizationOnboardingStatus.ClubSlugExists => Conflict(
				"A club with this slug already exists in the organization."),
			PlatformOrganizationOnboardingStatus.AdministratorEmailExists => Conflict(
				"A user with this administrator email already exists."),
			_ => StatusCode(StatusCodes.Status500InternalServerError)
		};
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<Organization>>> GetAll(
		CancellationToken cancellationToken) =>
		Ok(await organizations.GetAllAsync(cancellationToken));

	[HttpPost]
	public async Task<ActionResult<Organization>> Create(
		Organization organization,
		CancellationToken cancellationToken)
	{
		var error = Validate(organization);
		if (error is not null) return BadRequest(error);
		var created = await organizations.CreateAsync(organization, cancellationToken);
		return created is null
			? Conflict("An organization with this slug already exists.")
			: Created($"/api/platform/organizations/{created.Id}", created);
	}

	[HttpPut("{id:guid}")]
	public async Task<ActionResult<Organization>> Update(
		Guid id,
		Organization organization,
		CancellationToken cancellationToken)
	{
		var error = Validate(organization);
		if (error is not null) return BadRequest(error);
		var updated = await organizations.UpdateAsync(id, organization, cancellationToken);
		return updated is null
			? NotFound("Organization was not found or its slug is already in use.")
			: Ok(updated);
	}

	[HttpPatch("{id:guid}/active")]
	public async Task<ActionResult<Organization>> SetActive(
		Guid id,
		SetActiveRequest request,
		CancellationToken cancellationToken) =>
		await organizations.SetActiveAsync(id, request.IsActive, cancellationToken) is { } updated
			? Ok(updated)
			: NotFound();

	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(
		Guid id,
		CancellationToken cancellationToken)
	{
		var result = await organizations.DeleteAsync(id, cancellationToken);
		return result switch
		{
			OrganizationDeleteResult.Deleted => NoContent(),
			OrganizationDeleteResult.NotFound => NotFound(),
			OrganizationDeleteResult.HasClubs => Conflict(
				"Archive the organization instead. Permanent deletion is only available before clubs have been created."),
			_ => StatusCode(StatusCodes.Status500InternalServerError)
		};
	}

	private static string? Validate(Organization organization)
	{
		if (string.IsNullOrWhiteSpace(organization.Name)) return "Name is required.";
		if (organization.Name.Trim().Length > 100) return "Name must be 100 characters or fewer.";
		if (string.IsNullOrWhiteSpace(organization.Slug)) return "Slug is required.";
		if (!System.Text.RegularExpressions.Regex.IsMatch(
			organization.Slug.Trim(),
			"^[a-z0-9]+(?:-[a-z0-9]+)*$"))
			return "Slug must contain lowercase letters, numbers and single hyphens only.";
		return null;
	}

	private static string? ValidateOnboarding(PlatformOrganizationOnboardingInput input)
	{
		var organizationError = Validate(new Organization
		{
			Name = input.OrganizationName,
			Slug = input.OrganizationSlug
		});
		if (organizationError is not null) return organizationError;
		if (string.IsNullOrWhiteSpace(input.ClubName)) return "Club name is required.";
		if (input.ClubName.Trim().Length > 100) return "Club name must be 100 characters or fewer.";
		if (string.IsNullOrWhiteSpace(input.ClubSlug) || !IsSlug(input.ClubSlug))
			return "Club slug must contain lowercase letters, numbers and single hyphens only.";
		if (SportCatalog.Find(input.SportKey) is null) return "Sport is not supported.";
		if (!IsHexColor(input.PrimaryColor) || !IsHexColor(input.SecondaryColor))
			return "Club colours must use six-digit hex values.";
		if (!IsOptionalEmail(input.ClubContactEmail)) return "Club contact email is invalid.";
		if (!System.Net.Mail.MailAddress.TryCreate(input.AdministratorEmail?.Trim(), out _))
			return "Administrator email is invalid.";
		if (string.IsNullOrWhiteSpace(input.TemporaryPassword) || input.TemporaryPassword.Length < 8)
			return "Temporary password must be at least 8 characters long.";
		if (input.ClubAllowance is < 1 or > 100)
			return "Club allowance must be between 1 and 100.";
		if (!IsOptionalEmail(input.BillingEmail)) return "Billing email is invalid.";
		if (input.SubscriptionStatus is not SubscriptionStatus.Trialing and not SubscriptionStatus.Active)
			return "A new organization must start with a trialing or active subscription.";
		return null;
	}

	private static bool IsSlug(string value) =>
		System.Text.RegularExpressions.Regex.IsMatch(
			value.Trim(),
			"^[a-z0-9]+(?:-[a-z0-9]+)*$");

	private static bool IsHexColor(string value) =>
		System.Text.RegularExpressions.Regex.IsMatch(
			value?.Trim() ?? string.Empty,
			"^#[0-9a-fA-F]{6}$");

	private static bool IsOptionalEmail(string? value) =>
		string.IsNullOrWhiteSpace(value) ||
		System.Net.Mail.MailAddress.TryCreate(value.Trim(), out _);

	public sealed class SetActiveRequest
	{
		public bool IsActive { get; set; }
	}
}
