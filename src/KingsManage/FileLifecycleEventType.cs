namespace KingsManage;

public enum FileLifecycleEventType
{
	UploadRequested,
	UploadReused,
	UploadValidated,
	UploadRejected,
	FileQuarantined,
	ReferenceDeleted,
	AbandonedUploadDeleted,
	ReferenceCountReconciled,
	ExternalObjectDeleted,
	ExternalObjectDeletionFailed
}
