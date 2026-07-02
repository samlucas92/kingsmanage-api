using System.Text;
using System.Text.Json;

namespace KingsManage.Web.Services;

public static class RichTextBody
{
	private const string Prefix = "yepset-richtext:v1:";

	public static string ToPlainText(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		if (!value.StartsWith(Prefix, StringComparison.Ordinal))
		{
			return value.Trim();
		}

		try
		{
			using var document = JsonDocument.Parse(value[Prefix.Length..]);
			var builder = new StringBuilder();
			AppendText(document.RootElement, builder);
			return builder.ToString().Trim();
		}
		catch (JsonException)
		{
			return value.Trim();
		}
	}

	private static void AppendText(JsonElement element, StringBuilder builder)
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			if (
				element.TryGetProperty("type", out var type) &&
				type.GetString() == "image"
			)
			{
				builder.Append(
					element.TryGetProperty("alt", out var alt) &&
					!string.IsNullOrWhiteSpace(alt.GetString())
						? alt.GetString()
						: "Image"
				);
				return;
			}

			if (element.TryGetProperty("text", out var text))
			{
				builder.Append(text.GetString());
				return;
			}

			if (element.TryGetProperty("children", out var children))
			{
				AppendText(children, builder);
				builder.AppendLine();
			}
			return;
		}

		if (element.ValueKind != JsonValueKind.Array)
		{
			return;
		}

		foreach (var child in element.EnumerateArray())
		{
			AppendText(child, builder);
		}
	}
}
