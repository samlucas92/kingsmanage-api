namespace KingsManage;

public sealed class FileLifecycleRunResult
{
	public int AbandonedReferencesDeleted { get; set; }
	public int ReferenceCountsReconciled { get; set; }
	public int ExternalObjectsDeleted { get; set; }
	public int DeletionFailures { get; set; }
}
