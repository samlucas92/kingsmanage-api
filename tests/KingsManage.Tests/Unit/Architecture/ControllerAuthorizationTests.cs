using System.Reflection;
using KingsManage;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using NUnit.Framework;

namespace KingsManage.Tests.Unit.Architecture;

[TestFixture]
public class ControllerAuthorizationTests
{
	[Test]
	public void AuthController_Login_ShouldAllowAnonymous()
	{
		AssertMethodHasAttribute<AllowAnonymousAttribute>(typeof(AuthController), "Login");
	}

	[Test]
	public void AuthController_GetCurrentUser_ShouldRequireAuthenticatedUser()
	{
		AssertMethodHasAttribute<AuthorizeAttribute>(typeof(AuthController), "GetCurrentUser");
	}

	[Test]
	public void UsersController_ShouldBeOrganizationAdminOnly()
	{
		var authorizeAttributes = GetAuthorizeAttributes(typeof(UsersController));
		Assert.That(authorizeAttributes.Select(attribute => attribute.Policy), Does.Contain("OrganizationAdmin"));
	}

	[Test]
	public void PlatformOrganizationsController_ShouldBeSiteAdminOnly()
	{
		AssertControllerHasPolicy(typeof(PlatformOrganizationsController), "SiteAdmin");
	}

	[Test]
	public void FinanceController_ShouldRequireAuthorization()
	{
		var authorizeAttributes = GetAuthorizeAttributes(typeof(FinanceController));

		Assert.That(authorizeAttributes, Is.Not.Empty, "FinanceController should require authorization.");
	}

	[Test]
	public void FinanceController_WriteAndAdminReadActions_ShouldRequireClubAdmin()
	{
		AssertMethodHasPolicy(typeof(FinanceController), "GetSeasonFinance", "ClubAdmin");
		AssertMethodHasPolicy(typeof(FinanceController), "GetPlayerFinance", "ClubAdmin");
		AssertMethodHasPolicy(typeof(FinanceController), "AddTransaction", "ClubAdmin");
		AssertMethodHasPolicy(typeof(FinanceController), "SetPlayerAmountOwed", "ClubAdmin");
		AssertMethodHasPolicy(typeof(FinanceController), "DeleteTransaction", "ClubAdmin");
	}

	[Test]
	public void PlayersController_ShouldAllowAuthenticatedReadsAndManagerWrites()
	{
		var authorizeAttributes = GetAuthorizeAttributes(typeof(PlayersController));
		Assert.That(authorizeAttributes, Is.Not.Empty);
		Assert.That(GetRoles(authorizeAttributes), Is.Empty);
		AssertMethodHasPolicy(typeof(PlayersController), "Create", "TeamManagement");
		AssertMethodHasPolicy(typeof(PlayersController), "Update", "TeamManagement");
		AssertMethodHasPolicy(typeof(PlayersController), "SetActive", "TeamManagement");
	}

	[Test]
	public void MatchesController_ShouldRequireTeamManagement()
	{
		AssertControllerHasPolicy(typeof(MatchesController), "TeamManagement");
	}

	[Test]
	public void StatsController_ShouldRequireTeamManagement()
	{
		AssertControllerHasPolicy(typeof(StatsController), "TeamManagement");
	}

	[Test]
	public void FilesController_ShouldRequireAuthorization()
	{
		var authorizeAttributes = GetAuthorizeAttributes(typeof(FilesController));

		Assert.That(authorizeAttributes, Is.Not.Empty, "FilesController should require authorization.");
	}

	[Test]
	public void FilesController_WriteActions_ShouldAllowAdminAndCoachOnly()
	{
		AssertMethodHasPolicy(typeof(FilesController), "CreateUploadUrl", "TeamManagement");
		AssertMethodHasPolicy(typeof(FilesController), "MarkUploaded", "TeamManagement");
		AssertMethodHasPolicy(typeof(FilesController), "Delete", "TeamManagement");
	}

	[Test]
	public void SeasonsController_ShouldRequireAuthorization()
	{
		var authorizeAttributes = GetAuthorizeAttributes(typeof(SeasonsController));

		Assert.That(authorizeAttributes, Is.Not.Empty, "SeasonsController should require authorization.");
	}

	[Test]
	public void SeasonsController_WriteActions_ShouldRequireClubAdmin()
	{
		AssertMethodHasPolicy(typeof(SeasonsController), "Create", "ClubAdmin");
		AssertMethodHasPolicy(typeof(SeasonsController), "Update", "ClubAdmin");
		AssertMethodHasPolicy(typeof(SeasonsController), "SetActive", "ClubAdmin");
		AssertMethodHasPolicy(typeof(SeasonsController), "SetupSeason", "ClubAdmin");
	}

	private static void AssertControllerHasPolicy(Type controllerType, string policy)
	{
		var attributes = GetAuthorizeAttributes(controllerType);
		Assert.That(attributes.Select(attribute => attribute.Policy), Does.Contain(policy));
	}

	private static void AssertMethodHasPolicy(Type controllerType, string methodName, string policy)
	{
		var method = GetMethod(controllerType, methodName);
		var attributes = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray();
		Assert.That(attributes.Select(attribute => attribute.Policy), Does.Contain(policy));
	}

	private static void AssertMethodHasAttribute<TAttribute>(Type controllerType, string methodName)
		where TAttribute : Attribute
	{
		var method = GetMethod(controllerType, methodName);
		var hasAttribute = method.GetCustomAttributes<TAttribute>(inherit: true).Any();

		Assert.That(hasAttribute, Is.True, $"{controllerType.Name}.{methodName} should have {typeof(TAttribute).Name}.");
	}

	private static MethodInfo GetMethod(Type controllerType, string methodName)
	{
		var methods = controllerType
			.GetMethods(BindingFlags.Instance | BindingFlags.Public)
			.Where(method => method.Name == methodName)
			.ToArray();

		Assert.That(methods, Is.Not.Empty, $"{controllerType.Name}.{methodName} was not found.");

		return methods[0];
	}

	private static AuthorizeAttribute[] GetAuthorizeAttributes(Type controllerType)
	{
		return controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray();
	}

	private static string[] GetRoles(IEnumerable<AuthorizeAttribute> authorizeAttributes)
	{
		return authorizeAttributes
			.SelectMany(attribute => (attribute.Roles ?? string.Empty)
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			.OrderBy(role => role)
			.ToArray();
	}
}
