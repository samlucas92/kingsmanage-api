using System.Text;
using KingsManage;

namespace KingsManage.Web.Services;

public sealed class BasicFileContentScanner : IFileContentScanner
{
	private static readonly byte[] EicarMarker = Encoding.ASCII.GetBytes(
		"X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR"
	);
	private static readonly byte[] HtmlMarker = Encoding.ASCII.GetBytes("<!DOCTYPE html");
	private static readonly byte[] ScriptMarker = Encoding.ASCII.GetBytes("<script");

	public Task<FileContentScanResult> ScanAsync(
		ReadOnlyMemory<byte> content,
		string contentType,
		CancellationToken cancellationToken = default
	)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var bytes = content.Span;

		if (bytes.IndexOf(EicarMarker) >= 0)
		{
			return Task.FromResult(Unsafe("EICAR test malware signature"));
		}

		if (bytes.Length >= 2 && bytes[0] == (byte)'M' && bytes[1] == (byte)'Z')
		{
			return Task.FromResult(Unsafe("Executable content"));
		}

		if (bytes.IndexOf(HtmlMarker) >= 0 || bytes.IndexOf(ScriptMarker) >= 0)
		{
			return Task.FromResult(Unsafe("Active HTML or script content"));
		}

		return Task.FromResult(new FileContentScanResult { IsSafe = true });
	}

	private static FileContentScanResult Unsafe(string threatName)
	{
		return new FileContentScanResult
		{
			IsSafe = false,
			ThreatName = threatName
		};
	}
}
