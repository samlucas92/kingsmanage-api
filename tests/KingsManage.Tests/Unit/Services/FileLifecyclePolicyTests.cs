using KingsManage;

namespace KingsManage.Tests.Unit.Services;

[TestFixture]
public sealed class FileLifecyclePolicyTests
{
	private static readonly DateTime Now =
		new(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc);

	[Test]
	public void CountActiveReferences_CountsEachReferenceToTheSameObject()
	{
		var firstObjectId = Guid.NewGuid();
		var secondObjectId = Guid.NewGuid();

		var result = FileLifecyclePolicy.CountActiveReferences(
			[firstObjectId, firstObjectId, secondObjectId, null]
		);

		Assert.That(result[firstObjectId], Is.EqualTo(2));
		Assert.That(result[secondObjectId], Is.EqualTo(1));
	}

	[Test]
	public void IsOrphanReadyForDeletion_RequiresZeroReferencesAndElapsedRetention()
	{
		var storedObject = new StoredFileObject
		{
			ReferenceCount = 0,
			OrphanedAt = Now.AddDays(-8),
			Status = StoredFileObjectStatus.Uploaded
		};

		Assert.That(
			FileLifecyclePolicy.IsOrphanReadyForDeletion(
				storedObject,
				Now,
				TimeSpan.FromDays(7)
			),
			Is.True
		);

		storedObject.ReferenceCount = 1;
		Assert.That(
			FileLifecyclePolicy.IsOrphanReadyForDeletion(
				storedObject,
				Now,
				TimeSpan.FromDays(7)
			),
			Is.False
		);
	}

	[Test]
	public void ExpiryPolicies_DoNotExpireRecentPendingOrQuarantinedFiles()
	{
		var pending = new ClubFile
		{
			Status = ClubFileStatus.PendingUpload,
			CreatedAt = Now.AddHours(-2)
		};
		var quarantined = new ClubFile
		{
			Status = ClubFileStatus.Quarantined,
			QuarantinedAt = Now.AddHours(-2)
		};

		Assert.That(
			FileLifecyclePolicy.IsPendingUploadExpired(
				pending,
				Now,
				TimeSpan.FromHours(24)
			),
			Is.False
		);
		Assert.That(
			FileLifecyclePolicy.IsQuarantineExpired(
				quarantined,
				Now,
				TimeSpan.FromHours(72)
			),
			Is.False
		);
	}
}
