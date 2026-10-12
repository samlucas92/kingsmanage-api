using KingsManage;
using KingsManage.Web.Controllers;
using KingsManage.Web.Models;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public sealed class PlayerSponsorshipsControllerTests
{
	private const string TinyPng = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

	[Test]
	public async Task Submit_CompletesWindowAndLocksFurtherPublicEdits()
	{
		var service = new StubSponsorshipService(OpenSponsorship());
		var controller = CreateController(service);
		var request = ValidSubmission();

		var first = await controller.Submit("share-code", request, CancellationToken.None);
		var firstResult = first.Result as OkObjectResult;
		var firstModel = firstResult?.Value as PlayerSponsorshipViewModel;

		Assert.Multiple(() =>
		{
			Assert.That(firstResult, Is.Not.Null);
			Assert.That(firstModel?.Status, Is.EqualTo(PlayerSponsorshipStatus.Completed));
			Assert.That(firstModel?.TermsAccepted, Is.True);
			Assert.That(firstModel?.TermsText, Is.EqualTo(PlayerSponsorshipTerms.Text));
			Assert.That(service.Item.AcceptedTermsText, Is.EqualTo(PlayerSponsorshipTerms.Text));
		});

		var second = await controller.Submit("share-code", request, CancellationToken.None);
		Assert.That(second.Result, Is.TypeOf<ConflictObjectResult>());
	}

	[Test]
	public async Task Submit_WithoutAcceptedTerms_ReturnsBadRequest()
	{
		var service = new StubSponsorshipService(OpenSponsorship());
		var controller = CreateController(service);
		var request = ValidSubmission();
		request.TermsAccepted = false;

		var result = await controller.Submit("share-code", request, CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
		Assert.That(service.Item.SubmittedAt, Is.Null);
	}

	private static PlayerSponsorshipsController CreateController(IPlayerSponsorshipService service) =>
		new(service, null!, null!, null!, new BasicFileContentScanner());

	private static SubmitPlayerSponsorshipModel ValidSubmission() => new()
	{
		SponsorName = "Example sponsor",
		SponsorDescription = "A local business supporting the player.",
		SponsorImageDataUrl = TinyPng,
		TermsAccepted = true
	};

	private static PlayerSponsorship OpenSponsorship() => new()
	{
		Id = Guid.NewGuid(),
		OrganizationId = Guid.NewGuid(),
		ClubId = Guid.NewGuid(),
		PlayerId = Guid.NewGuid(),
		PlayerName = "Alex Player",
		GoCode = "share-code",
		Status = PlayerSponsorshipStatus.Open
	};

	private sealed class StubSponsorshipService(PlayerSponsorship item) : IPlayerSponsorshipService
	{
		public PlayerSponsorship Item { get; } = item;

		public Task<PlayerSponsorship?> GetByGoCodeAsync(string goCode, CancellationToken cancellationToken = default) =>
			Task.FromResult<PlayerSponsorship?>(goCode == Item.GoCode ? Item : null);

		public Task<PlayerSponsorship?> SubmitAsync(PlayerSponsorship sponsorship, CancellationToken cancellationToken = default) =>
			Task.FromResult<PlayerSponsorship?>(sponsorship);

		public Task<IReadOnlyList<PlayerSponsorship>> GetAllAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<PlayerSponsorship>>([Item]);

		public Task<PlayerSponsorship?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult<PlayerSponsorship?>(id == Item.Id ? Item : null);

		public Task<PlayerSponsorship> CreateAsync(PlayerSponsorship sponsorship, CancellationToken cancellationToken = default) =>
			Task.FromResult(sponsorship);

		public Task<PlayerSponsorship?> UpdateAsync(PlayerSponsorship sponsorship, CancellationToken cancellationToken = default) =>
			Task.FromResult<PlayerSponsorship?>(sponsorship);

		public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult(id == Item.Id);
	}
}
