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
	private TestClubFileService files = null!;
	private TestStoredFileObjectService objects = null!;
	private RichTextAssetService service = null!;

	[SetUp]
	public void SetUp()
	{
		files = new TestClubFileService();
		objects = new TestStoredFileObjectService();
		objects.Objects.Add(new StoredFileObject
		{
			Id = StoredObjectId,
			ContentHash = new string('a', 64),
			ContentType = "image/png",
			SizeBytes = 100,
			Status = StoredFileObjectStatus.Uploaded,
			ReferenceCount = 1
		});
		service = new RichTextAssetService(files, objects);
	}

	[Test]
	public async Task SynchronizeAsync_PromotesDraftReferenceAndRewritesImageNode()
	{
		files.Files.Add(CreateSource(ClubFileLinkedEntityType.RichTextDraft));

		var result = await service.SynchronizeAsync(
			CreateBody(SourceFileId), null, ClubFileLinkedEntityType.Post, TargetId,
			Guid.NewGuid(), "admin@example.com");

		var newFileId = ReadImageFileId(result);
		Assert.That(newFileId, Is.Not.EqualTo(SourceFileId));
		Assert.That(files.Files.Single(file => file.Id == SourceFileId).Status, Is.EqualTo(ClubFileStatus.Deleted));
		Assert.That(files.Files.Single(file => file.Id == newFileId).LinkedEntityType, Is.EqualTo(ClubFileLinkedEntityType.Post));
		Assert.That(objects.Objects.Single().ReferenceCount, Is.EqualTo(1));
	}

	[Test]
	public async Task SynchronizeAsync_ClonesTemplateReferenceWithoutDeletingTemplate()
	{
		files.Files.Add(CreateSource(ClubFileLinkedEntityType.PostTemplate));

		var result = await service.SynchronizeAsync(
			CreateBody(SourceFileId), null, ClubFileLinkedEntityType.Post, TargetId,
			Guid.NewGuid(), "admin@example.com");

		Assert.That(ReadImageFileId(result), Is.Not.EqualTo(SourceFileId));
		Assert.That(files.Files.Single(file => file.Id == SourceFileId).Status, Is.EqualTo(ClubFileStatus.Uploaded));
		Assert.That(objects.Objects.Single().ReferenceCount, Is.EqualTo(2));
	}

	[Test]
	public async Task SynchronizeAsync_RemovingTargetImageDeletesOnlyItsReference()
	{
		files.Files.Add(CreateSource(ClubFileLinkedEntityType.Post));
		const string emptyBody =
			"yepset-richtext:v1:[{\"type\":\"paragraph\",\"children\":[{\"text\":\"Updated\"}]}]";

		await service.SynchronizeAsync(
			emptyBody, CreateBody(SourceFileId), ClubFileLinkedEntityType.Post,
			TargetId, Guid.NewGuid(), "admin@example.com");

		Assert.That(files.Files.Single().Status, Is.EqualTo(ClubFileStatus.Deleted));
		Assert.That(objects.Objects.Single().ReferenceCount, Is.Zero);
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
