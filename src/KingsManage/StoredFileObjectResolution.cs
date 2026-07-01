namespace KingsManage;

public sealed record StoredFileObjectResolution(
	StoredFileObject StoredObject,
	bool WasCreated
);
