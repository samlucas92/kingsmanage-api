namespace KingsManage;

public interface IFileContentScanner
{
	Task<FileContentScanResult> ScanAsync(
		ReadOnlyMemory<byte> content,
		string contentType,
		CancellationToken cancellationToken = default
	);
}
