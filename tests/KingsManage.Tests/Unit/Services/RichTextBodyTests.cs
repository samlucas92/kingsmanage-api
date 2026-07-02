using KingsManage.Web.Services;
using NUnit.Framework;

namespace KingsManage.Tests.Unit.Services;

public class RichTextBodyTests
{
	[Test]
	public void ToPlainText_ReturnsLegacyPlainText()
	{
		Assert.That(RichTextBody.ToPlainText(" Existing message "), Is.EqualTo("Existing message"));
	}

	[Test]
	public void ToPlainText_ExtractsTextFromSlateDocument()
	{
		const string value =
			"yepset-richtext:v1:[{\"type\":\"heading-two\",\"children\":[{\"text\":\"Matchday\",\"bold\":true}]},{\"type\":\"paragraph\",\"children\":[{\"text\":\"Meet at 1pm\"}]}]";

		Assert.That(
			RichTextBody.ToPlainText(value),
			Is.EqualTo($"Matchday{Environment.NewLine}Meet at 1pm")
		);
	}

	[Test]
	public void ToPlainText_RecognisesAnEmptySlateDocument()
	{
		const string value =
			"yepset-richtext:v1:[{\"type\":\"paragraph\",\"children\":[{\"text\":\"\"}]}]";

		Assert.That(RichTextBody.ToPlainText(value), Is.Empty);
	}

	[Test]
	public void ToPlainText_UsesEmbeddedImageAlternativeText()
	{
		const string value =
			"yepset-richtext:v1:[{\"type\":\"image\",\"fileId\":\"00000000-0000-0000-0000-000000000001\",\"alt\":\"Team celebration\",\"children\":[{\"text\":\"\"}]}]";

		Assert.That(RichTextBody.ToPlainText(value), Is.EqualTo("Team celebration"));
	}
}
