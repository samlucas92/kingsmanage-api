using System.Text.Json;
using KingsManage;
using KingsManage.Tests.Integration.Auth;
using KingsManage.Web.Services;

namespace KingsManage.Tests.Unit.Services;

[TestFixture]
public sealed class RichTextAssetServiceTests
{
	private static readonly Guid StoredObjectId = Guid.NewGuid();
	private static readonly Guid SourceFileId = Guid.NewGuid();
	private static readonly Guid TargetId = Guid.NewGuid();
	private TestClubFileService _files = null!;
	private TestStoredFileObjectService _objects = null!;
	private RichTextAssetService _service = null!;

	[SetUp]
	public void SetUp()
	{
		_files = new TestClubFileService();
		_objects = new TestStoredFileObjectService();
		_objects.Objects.Add(new StoredFileObject
		{
			Id = StoredObjectId,
			ContentHash = new string('a', 64),
			ContentType = "image/png",
			SizeBytes = 100,
			Status = StoredFileObjectStatus.Uploaded,
			ReferenceCount = 1
		});
		_service = new RichTextAssetService(_files, _objects);
	}

	[Test]
	public async Task SynchronizeAsync_PromotesDraftReferenceAndRewritesImageNode()
	{
		_files.Files.Add(CreateSource(ClubFileLinkedEntityType.RichTextDraft));

		var result = await _service.SynchronizeAsync(
			CreateBody(SourceFileId), null, ClubFileLinkedEntityType.Post, TargetId,
			Guid.NewGuid(), "admin@example.com");

		var newFileId = ReadImageFileId(result);
		Assert.That(newFileId, Is.Not.EqualTo(SourceFileId));
		Assert.That(_files.Files.Single(file => file.Id == SourceFileId).Status, Is.EqualTo(ClubFileStatus.Deleted));
		Assert.That(_files.Files.Single(file => file.Id == newFileId).LinkedEntityType, Is.EqualTo(ClubFileLinkedEntityType.Post));
		Assert.That(_objects.Objects.Single().ReferenceCount, Is.EqualTo(1));
	}

	[Test]
	public async Task SynchronizeAsync_ClonesTemplateReferenceWithoutDeletingTemplate()
	{
		_files.Files.Add(CreateSource(ClubFileLinkedEntityType.PostTemplate));

		var result = await _service.SynchronizeAsync(
			CreateBody(SourceFileId), null, ClubFileLinkedEntityType.Post, TargetId,
			Guid.NewGuid(), "admin@example.com");

		Assert.That(ReadImageFileId(result), Is.Not.EqualTo(SourceFileId));
		Assert.That(_files.Files.Single(file => file.Id == SourceFileId).Status, Is.EqualTo(ClubFileStatus.Uploaded));
		Assert.That(_objects.Objects.Single().ReferenceCount, Is.EqualTo(2));
	}

	[Test]
	public async Task SynchronizeAsync_RemovingTargetImageDeletesOnlyItsReference()
	{
		_files.Files.Add(CreateSource(ClubFileLinkedEntityType.Post));
		const string emptyBody =
			"yepset-richtext:v1:[{\"type\":\"paragraph\",\"children\":[{\"text\":\"Updated\"}]}]";

		await _service.SynchronizeAsync(
			emptyBody, CreateBody(SourceFileId), ClubFileLinkedEntityType.Post,
			TargetId, Guid.NewGuid(), "admin@example.com");

		Assert.That(_files.Files.Single().Status, Is.EqualTo(ClubFileStatus.Deleted));
		Assert.That(_objects.Objects.Single().ReferenceCount, Is.Zero);
	}

	private static ClubFile CreateSource(ClubFileLinkedEntityType linkedEntityType) => new()
	{
		Id = SourceFileId,
		StoredObjectId = StoredObjectId,
		StorageKey = "objects/image",
		OriginalFileName = "image.png",
		StoredFileName = "image.png",
		ContentHash = new string('a', 64),
		ContentType = "image/png",
		SizeBytes = 100,
		LinkedEntityType = linkedEntityType,
		LinkedEntityId = linkedEntityType == ClubFileLinkedEntityType.Post ? TargetId : Guid.NewGuid(),
		Status = ClubFileStatus.Uploaded
	};

	private static string CreateBody(Guid fileId) =>
		$"yepset-richtext:v1:[{{\"type\":\"image\",\"fileId\":\"{fileId}\",\"alt\":\"Team photo\",\"children\":[{{\"text\":\"\"}}]}}]";

	private static Guid ReadImageFileId(string body)
	{
		using var document = JsonDocument.Parse(body["yepset-richtext:v1:".Length..]);
		return document.RootElement[0].GetProperty("fileId").GetGuid();
	}
}
