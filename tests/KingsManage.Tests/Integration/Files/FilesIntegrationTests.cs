using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KingsManage;
using KingsManage.Tests.Integration.Auth;
using NUnit.Framework;

namespace KingsManage.Tests.Integration.Files;

[TestFixture]
public sealed class FilesIntegrationTests
{
	private AuthIntegrationTestFactory _factory = null!;

	[SetUp]
	public void SetUp()
	{
		_factory = new AuthIntegrationTestFactory();
		_factory.SeedDefaultUsers();
	}

	[TearDown]
	public void TearDown()
	{
		_factory.Dispose();
	}

	[Test]
	public async Task CreateUploadUrl_AsCoach_CreatesPendingFileAndUploadUrl()
	{
		var postId = SeedPost();
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/files/upload-url",
			new
			{
				OriginalFileName = "Team Sheet.pdf",
				ContentType = "application/pdf",
				SizeBytes = 2048,
				LinkedEntityType = "Post",
				LinkedEntityId = postId,
				Visibility = "AuthenticatedUsers"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		Assert.That(_factory.ClubFileService.Files, Has.Count.EqualTo(1));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var root = document.RootElement;
		var file = root.GetProperty("file");

		Assert.That(root.GetProperty("uploadUrl").GetString(), Does.Contain("https://storage.test/upload/"));
		Assert.That(file.GetProperty("originalFileName").GetString(), Is.EqualTo("Team Sheet.pdf"));
		Assert.That(file.GetProperty("storedFileName").GetString(), Is.EqualTo("Team-Sheet.pdf"));
		Assert.That(file.GetProperty("status").GetString(), Is.EqualTo("PendingUpload"));
		Assert.That(file.GetProperty("uploadedByUserEmail").GetString(), Is.EqualTo(TestUsers.CoachEmail));
		Assert.That(
			_factory.FileLifecycleService.Audit.Single().EventType,
			Is.EqualTo(FileLifecycleEventType.UploadRequested)
		);
	}

	[Test]
	public async Task CreateUploadUrl_AsPlayer_ReturnsForbidden()
	{
		var postId = SeedPost();
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/files/upload-url",
			new
			{
				OriginalFileName = "Player upload.pdf",
				ContentType = "application/pdf",
				SizeBytes = 2048,
				LinkedEntityType = "Post",
				LinkedEntityId = postId,
				Visibility = "AuthenticatedUsers"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
		Assert.That(_factory.ClubFileService.Files, Is.Empty);
	}

	[Test]
	public async Task CreateUploadUrl_WithUploadedMatchingHash_ReusesStoredObject()
	{
		var postId = SeedPost();
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);
		const string contentHash =
			"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

		var firstResponse = await client.PostAsJsonAsync(
			"/api/files/upload-url",
			new
			{
				OriginalFileName = "First.pdf",
				ContentType = "application/pdf",
				SizeBytes = 2048,
				ContentHash = contentHash,
				LinkedEntityType = "Post",
				LinkedEntityId = postId,
				Visibility = "AuthenticatedUsers"
			}
		);

		Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		using var firstDocument = JsonDocument.Parse(
			await firstResponse.Content.ReadAsStringAsync()
		);
		var firstFileId = firstDocument.RootElement
			.GetProperty("file")
			.GetProperty("id")
			.GetGuid();
		Assert.That(firstDocument.RootElement.GetProperty("uploadRequired").GetBoolean(), Is.True);

		var markedResponse = await client.PostAsync(
			$"/api/files/{firstFileId}/mark-uploaded",
			null
		);
		Assert.That(markedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		var secondResponse = await client.PostAsJsonAsync(
			"/api/files/upload-url",
			new
			{
				OriginalFileName = "Second-copy.pdf",
				ContentType = "application/pdf",
				SizeBytes = 2048,
				ContentHash = contentHash,
				LinkedEntityType = "Post",
				LinkedEntityId = postId,
				Visibility = "AuthenticatedUsers"
			}
		);

		Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		using var secondDocument = JsonDocument.Parse(
			await secondResponse.Content.ReadAsStringAsync()
		);
		Assert.That(secondDocument.RootElement.GetProperty("uploadRequired").GetBoolean(), Is.False);
		Assert.That(secondDocument.RootElement.GetProperty("reusedStoredObject").GetBoolean(), Is.True);
		Assert.That(secondDocument.RootElement.GetProperty("uploadUrl").GetString(), Is.Empty);
		Assert.That(
			secondDocument.RootElement.GetProperty("file").GetProperty("status").GetString(),
			Is.EqualTo("Uploaded")
		);
		Assert.That(_factory.StoredFileObjectService.Objects, Has.Count.EqualTo(1));
		Assert.That(_factory.StoredFileObjectService.Objects.Single().ReferenceCount, Is.EqualTo(2));
		Assert.That(_factory.ClubFileService.Files, Has.Count.EqualTo(2));
		Assert.That(
			_factory.ClubFileService.Files.Select(file => file.StorageKey).Distinct().Count(),
			Is.EqualTo(1)
		);

		var secondFileId = secondDocument.RootElement
			.GetProperty("file")
			.GetProperty("id")
			.GetGuid();
		var deleteResponse = await client.DeleteAsync($"/api/files/{secondFileId}");

		Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
		Assert.That(_factory.StoredFileObjectService.Objects.Single().ReferenceCount, Is.EqualTo(1));
	}

	[Test]
	public async Task CreateUploadUrl_WithMalformedHash_ReturnsBadRequest()
	{
		var postId = SeedPost();
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/files/upload-url",
			new
			{
				OriginalFileName = "Invalid.pdf",
				ContentType = "application/pdf",
				SizeBytes = 2048,
				ContentHash = "not-a-sha256",
				LinkedEntityType = "Post",
				LinkedEntityId = postId,
				Visibility = "AuthenticatedUsers"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
		Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("SHA-256"));
	}

	[Test]
	public async Task CreateUploadUrl_WithInvalidContentType_ReturnsBadRequest()
	{
		var postId = SeedPost();
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/files/upload-url",
			new
			{
				OriginalFileName = "notes.txt",
				ContentType = "text/plain",
				SizeBytes = 2048,
				LinkedEntityType = "Post",
				LinkedEntityId = postId,
				Visibility = "AuthenticatedUsers"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
		Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("File type is not allowed."));
	}

	[Test]
	public async Task CreateUploadUrl_WhenOrganizationQuotaIsExceeded_ReturnsPayloadTooLarge()
	{
		var postId = SeedPost();
		_factory.FileLifecycleService.UploadAllowed = false;
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/files/upload-url",
			new
			{
				OriginalFileName = "Full.pdf",
				ContentType = "application/pdf",
				SizeBytes = 2048,
				LinkedEntityType = "Post",
				LinkedEntityId = postId,
				Visibility = "AuthenticatedUsers"
			}
		);

		Assert.That(
			response.StatusCode,
			Is.EqualTo(HttpStatusCode.RequestEntityTooLarge)
		);
		Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("quota exceeded"));
		Assert.That(_factory.ClubFileService.Files, Is.Empty);
		Assert.That(
			_factory.FileLifecycleService.Audit.Single().EventType,
			Is.EqualTo(FileLifecycleEventType.UploadRejected)
		);
	}

	[Test]
	public async Task MarkUploaded_AsAdmin_MarksPendingFileUploaded()
	{
		var fileId = SeedPendingFile();
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsync($"/api/files/{fileId}/mark-uploaded", null);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		Assert.That(_factory.ClubFileService.Files.Single().Status, Is.EqualTo(ClubFileStatus.Uploaded));
		Assert.That(_factory.ClubFileService.Files.Single().UploadedAt, Is.Not.Null);
		Assert.That(_factory.FileStorageService.ValidatedStorageKeys, Has.Count.EqualTo(1));
	}

	[Test]
	public async Task MarkUploaded_WhenStoredChecksumDoesNotMatch_LeavesFilePending()
	{
		var postId = SeedPost();
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);
		const string contentHash =
			"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		var uploadResponse = await client.PostAsJsonAsync(
			"/api/files/upload-url",
			new
			{
				OriginalFileName = "Invalid.pdf",
				ContentType = "application/pdf",
				SizeBytes = 2048,
				ContentHash = contentHash,
				LinkedEntityType = "Post",
				LinkedEntityId = postId,
				Visibility = "AuthenticatedUsers"
			}
		);

		Assert.That(uploadResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		using var document = JsonDocument.Parse(
			await uploadResponse.Content.ReadAsStringAsync()
		);
		var fileId = document.RootElement
			.GetProperty("file")
			.GetProperty("id")
			.GetGuid();
		_factory.FileStorageService.ValidationResult = new FileStorageValidationResult
		{
			IsValid = false,
			ErrorMessage = "Stored object checksum does not match the upload."
		};

		var response = await client.PostAsync(
			$"/api/files/{fileId}/mark-uploaded",
			null
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
		Assert.That(
			await response.Content.ReadAsStringAsync(),
			Does.Contain("checksum does not match")
		);
		Assert.That(
			_factory.ClubFileService.Files.Single().Status,
			Is.EqualTo(ClubFileStatus.PendingUpload)
		);
		Assert.That(
			_factory.StoredFileObjectService.Objects.Single().Status,
			Is.EqualTo(StoredFileObjectStatus.PendingUpload)
		);
	}

	[Test]
	public async Task MarkUploaded_WhenSecurityScanFails_QuarantinesFileAndObject()
	{
		var postId = SeedPost();
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);
		const string contentHash =
			"cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
		var uploadResponse = await client.PostAsJsonAsync(
			"/api/files/upload-url",
			new
			{
				OriginalFileName = "Unsafe.pdf",
				ContentType = "application/pdf",
				SizeBytes = 2048,
				ContentHash = contentHash,
				LinkedEntityType = "Post",
				LinkedEntityId = postId,
				Visibility = "AuthenticatedUsers"
			}
		);
		using var document = JsonDocument.Parse(
			await uploadResponse.Content.ReadAsStringAsync()
		);
		var fileId = document.RootElement
			.GetProperty("file")
			.GetProperty("id")
			.GetGuid();
		_factory.FileStorageService.ValidationResult = new FileStorageValidationResult
		{
			IsValid = true,
			IsSafe = false,
			ThreatName = "EICAR test malware signature"
		};

		var response = await client.PostAsync(
			$"/api/files/{fileId}/mark-uploaded",
			null
		);

		Assert.That(
			response.StatusCode,
			Is.EqualTo(HttpStatusCode.UnprocessableEntity)
		);
		Assert.That(
			_factory.ClubFileService.Files.Single().Status,
			Is.EqualTo(ClubFileStatus.Quarantined)
		);
		Assert.That(
			_factory.StoredFileObjectService.Objects.Single().Status,
			Is.EqualTo(StoredFileObjectStatus.Quarantined)
		);
		Assert.That(
			_factory.FileLifecycleService.Audit.Last().EventType,
			Is.EqualTo(FileLifecycleEventType.FileQuarantined)
		);
	}

	[Test]
	public async Task GetStorageUsage_AsAdmin_ReturnsOrganizationUsage()
	{
		_factory.FileLifecycleService.Usage = new FileStorageUsage
		{
			UsedBytes = 800,
			QuotaBytes = 1000,
			RemainingBytes = 200,
			UsedPercent = 80,
			IsNearLimit = true
		};
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.GetAsync("/api/files/storage-usage");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		Assert.That(document.RootElement.GetProperty("usedBytes").GetInt64(), Is.EqualTo(800));
		Assert.That(document.RootElement.GetProperty("isNearLimit").GetBoolean(), Is.True);
	}

	[Test]
	public async Task GetStorageUsage_AsPlayer_ReturnsForbidden()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync("/api/files/storage-usage");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task CreateDownloadUrl_ForUploadedPostFile_AsPlayer_ReturnsDownloadUrl()
	{
		var fileId = SeedUploadedFile();
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync($"/api/files/{fileId}/download-url");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var root = document.RootElement;

		Assert.That(root.GetProperty("downloadUrl").GetString(), Does.Contain("https://storage.test/download/"));
		Assert.That(root.GetProperty("file").GetProperty("status").GetString(), Is.EqualTo("Uploaded"));
	}

	[Test]
	public async Task CreateDownloadUrl_ForPendingFile_ReturnsNotFound()
	{
		var fileId = SeedPendingFile();
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync($"/api/files/{fileId}/download-url");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
	}

	[Test]
	public async Task Delete_AsAdmin_SoftDeletesFile()
	{
		var fileId = SeedUploadedFile();
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.DeleteAsync($"/api/files/{fileId}");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

		var file = _factory.ClubFileService.Files.Single();
		Assert.That(file.Status, Is.EqualTo(ClubFileStatus.Deleted));
		Assert.That(file.DeletedByUserId, Is.EqualTo(TestUsers.AdminId));
		Assert.That(file.DeletedAt, Is.Not.Null);
	}

	private Guid SeedPost()
	{
		var postId = Guid.Parse("80000000-0000-0000-0000-000000000101");

		if (_factory.ClubPostService.Posts.All(post => post.Id != postId))
		{
			_factory.ClubPostService.Posts.Add(
				new ClubPost
				{
					Id = postId,
					Type = ClubPostType.General,
					Title = "Post with attachment",
					Body = "Attachment test post.",
					CreatedByUserId = TestUsers.AdminId,
					CreatedByUserEmail = TestUsers.AdminEmail,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				}
			);
		}

		return postId;
	}

	private Guid SeedPendingFile()
	{
		var postId = SeedPost();
		var fileId = Guid.Parse("90000000-0000-0000-0000-000000000001");

		_factory.ClubFileService.Files.Add(
			new ClubFile
			{
				Id = fileId,
				OriginalFileName = "Fixture.pdf",
				StoredFileName = "Fixture.pdf",
				StorageKey = $"post/{postId}/{fileId}/Fixture.pdf",
				ContentType = "application/pdf",
				SizeBytes = 1024,
				Visibility = ClubFileVisibility.AuthenticatedUsers,
				LinkedEntityType = ClubFileLinkedEntityType.Post,
				LinkedEntityId = postId,
				Status = ClubFileStatus.PendingUpload,
				UploadedByUserId = TestUsers.AdminId,
				UploadedByUserEmail = TestUsers.AdminEmail,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			}
		);

		return fileId;
	}

	private Guid SeedUploadedFile()
	{
		var fileId = SeedPendingFile();
		var file = _factory.ClubFileService.Files.Single(currentFile => currentFile.Id == fileId);

		file.Status = ClubFileStatus.Uploaded;
		file.UploadedAt = DateTime.UtcNow;

		return fileId;
	}
}
