using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

[TestFixture]
public sealed class SportsControllerTests
{
	[Test]
	public void GetAll_ReturnsSupportedMultiSportDefinitions()
	{
		var result = new SportsController().GetAll();
		var sports = (result.Result as OkObjectResult)?.Value as IReadOnlyList<KingsManage.SportDefinition>;

		Assert.That(sports?.Select(sport => sport.Key), Is.SupersetOf(new[] { "football", "rugby-union", "cricket", "hockey", "netball" }));
		Assert.That(sports?.Single(sport => sport.Key == "netball").PlayersPerSide, Is.EqualTo(7));
	}

	[Test]
	public void Get_UnknownSport_ReturnsNotFound()
	{
		var result = new SportsController().Get("quidditch");
		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}
}
