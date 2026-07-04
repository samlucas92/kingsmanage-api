using System.Text.Json;
using System.Text.Json.Nodes;
using KingsManage;

namespace KingsManage.Web.Services;

public sealed class RichTextAssetService
{
	private const string Prefix = "yepset-richtext:v1:";
	private readonly IClubFileService files;
	private readonly IStoredFileObjectService storedObjects;

	public RichTextAssetService(
		IClubFileService files,
		IStoredFileObjectService storedObjects
	)
	{
		this.files = files;
		this.storedObjects = storedObjects;
	}

	public async Task<string> SynchronizeAsync(
		string body,
		string? previousBody,
		ClubFileLinkedEntityType targetType,
		Guid targetId,
		Guid userId,
		string userEmail,
		CancellationToken cancellationToken = default
	)
	{
		var document = Parse(body);
		if (document is null)
		{
			return body;
		}

		var imageNodes = FindImageNodes(document).ToList();
		var replacements = new Dictionary<Guid, Guid>();

		foreach (var node in imageNodes)
		{
			if (!TryGetFileId(node, out var sourceId))
			{
				throw new InvalidOperationException("An embedded image has an invalid file reference.");
			}

			if (replacements.TryGetValue(sourceId, out var replacementId))
			{
				node["fileId"] = replacementId.ToString();
				continue;
			}

			var source = await files.GetByIdAsync(sourceId, cancellationToken);
			if (source is null || source.Status != ClubFileStatus.Uploaded)
			{
				throw new InvalidOperationException("An embedded image is unavailable.");
			}

			if (
				source.LinkedEntityType == targetType &&
				source.LinkedEntityId == targetId
			)
			{
				replacements[sourceId] = sourceId;
				continue;
			}

			if (source.StoredObjectId is not Guid storedObjectId)
			{
				throw new InvalidOperationException("An embedded image is not managed by file storage.");
			}

			if (!await storedObjects.IncrementReferenceCountAsync(storedObjectId, cancellationToken))
			{
				throw new InvalidOperationException("An embedded image is currently unavailable.");
			}

			var clone = await files.CreateAsync(
				new ClubFile
				{
					Id = Guid.NewGuid(),
					OriginalFileName = source.OriginalFileName,
					StoredFileName = source.StoredFileName,
					StorageKey = source.StorageKey,
					StoredObjectId = source.StoredObjectId,
					ContentHash = source.ContentHash,
					ContentType = source.ContentType,
					SizeBytes = source.SizeBytes,
					Visibility = ClubFileVisibility.AuthenticatedUsers,
					LinkedEntityType = targetType,
					LinkedEntityId = targetId,
					Status = ClubFileStatus.Uploaded,
					UploadedByUserId = userId,
					UploadedByUserEmail = userEmail,
					UploadedAt = DateTime.UtcNow
				},
				cancellationToken
			);

			replacements[sourceId] = clone.Id;
			node["fileId"] = clone.Id.ToString();

			if (source.LinkedEntityType == ClubFileLinkedEntityType.RichTextDraft)
			{
				await DeleteReferenceAsync(source, userId, cancellationToken);
			}
		}

		var synchronizedBody = $"{Prefix}{document.ToJsonString()}";
		await DeleteRemovedTargetReferencesAsync(
			previousBody,
			synchronizedBody,
			targetType,
			targetId,
			userId,
			cancellationToken
		);
		return synchronizedBody;
	}

	public async Task DeleteAllAsync(
		string? body,
		ClubFileLinkedEntityType targetType,
		Guid targetId,
		Guid userId,
		CancellationToken cancellationToken = default
	)
	{
		var fileIds = GetImageFileIds(body);
		foreach (var fileId in fileIds)
		{
			var file = await files.GetByIdAsync(fileId, cancellationToken);
			if (
				file is not null &&
				file.LinkedEntityType == targetType &&
				file.LinkedEntityId == targetId
			)
			{
				await DeleteReferenceAsync(file, userId, cancellationToken);
			}
		}
	}

	private async Task DeleteRemovedTargetReferencesAsync(
		string? previousBody,
		string currentBody,
		ClubFileLinkedEntityType targetType,
		Guid targetId,
		Guid userId,
		CancellationToken cancellationToken
	)
	{
		var currentIds = GetImageFileIds(currentBody);
		foreach (var previousId in GetImageFileIds(previousBody).Except(currentIds))
		{
			var previous = await files.GetByIdAsync(previousId, cancellationToken);
			if (
				previous is not null &&
				previous.LinkedEntityType == targetType &&
				previous.LinkedEntityId == targetId
			)
			{
				await DeleteReferenceAsync(previous, userId, cancellationToken);
			}
		}
	}

	private async Task DeleteReferenceAsync(
		ClubFile file,
		Guid userId,
		CancellationToken cancellationToken
	)
	{
		if (!await files.SoftDeleteAsync(file.Id, userId, cancellationToken))
		{
			return;
		}

		if (file.StoredObjectId is Guid storedObjectId)
		{
			await storedObjects.DecrementReferenceCountAsync(
				storedObjectId,
				cancellationToken
			);
		}
	}

	private static HashSet<Guid> GetImageFileIds(string? body)
	{
		var document = Parse(body);
		if (document is null)
		{
			return [];
		}

		return FindImageNodes(document)
			.Select(node => TryGetFileId(node, out var id) ? id : Guid.Empty)
			.Where(id => id != Guid.Empty)
			.ToHashSet();
	}

	private static JsonArray? Parse(string? body)
	{
		if (string.IsNullOrWhiteSpace(body) || !body.StartsWith(Prefix, StringComparison.Ordinal))
		{
			return null;
		}

		try
		{
			return JsonNode.Parse(body[Prefix.Length..]) as JsonArray;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static IEnumerable<JsonObject> FindImageNodes(JsonNode node)
	{
		if (node is JsonObject current)
		{
			if (string.Equals(current["type"]?.GetValue<string>(), "image", StringComparison.Ordinal))
			{
				yield return current;
			}

			foreach (var child in current)
			{
				if (child.Value is null)
				{
					continue;
				}

				foreach (var image in FindImageNodes(child.Value))
				{
					yield return image;
				}
			}
		}
		else if (node is JsonArray array)
		{
			foreach (var child in array)
			{
				if (child is null)
				{
					continue;
				}

				foreach (var image in FindImageNodes(child))
				{
					yield return image;
				}
			}
		}
	}

	private static bool TryGetFileId(JsonObject node, out Guid fileId)
	{
		return Guid.TryParse(node["fileId"]?.GetValue<string>(), out fileId);
	}
}
