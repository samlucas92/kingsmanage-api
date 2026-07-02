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
	public void FinanceController_ShouldRequireAuthorization()
	{
		var authorizeAttributes = GetAuthorizeAttributes(typeof(FinanceController));

		Assert.That(authorizeAttributes, Is.Not.Empty, "FinanceController should require authorization.");
	}

	[Test]
	public void FinanceController_WriteAndAdminReadActions_ShouldBeAdminOnly()
	{
		AssertMethodHasRoles(typeof(FinanceController), "GetSeasonFinance", UserRole.Admin);
		AssertMethodHasRoles(typeof(FinanceController), "GetPlayerFinance", UserRole.Admin);
		AssertMethodHasRoles(typeof(FinanceController), "AddTransaction", UserRole.Admin);
		AssertMethodHasRoles(typeof(FinanceController), "SetPlayerAmountOwed", UserRole.Admin);
		AssertMethodHasRoles(typeof(FinanceController), "DeleteTransaction", UserRole.Admin);
	}

	[Test]
	public void PlayersController_ShouldAllowAuthenticatedReadsAndManagerWrites()
	{
		var authorizeAttributes = GetAuthorizeAttributes(typeof(PlayersController));
		Assert.That(authorizeAttributes, Is.Not.Empty);
		Assert.That(GetRoles(authorizeAttributes), Is.Empty);
		AssertMethodHasRoles(typeof(PlayersController), "Create", UserRole.Admin, UserRole.Coach);
		AssertMethodHasRoles(typeof(PlayersController), "Update", UserRole.Admin, UserRole.Coach);
		AssertMethodHasRoles(typeof(PlayersController), "SetActive", UserRole.Admin, UserRole.Coach);
	}

	[Test]
	public void MatchesController_ShouldAllowAdminAndCoachOnly()
	{
		AssertControllerHasRoles(typeof(MatchesController), UserRole.Admin, UserRole.Coach);
	}

	[Test]
	public void StatsController_ShouldAllowAdminAndCoachOnly()
	{
		AssertControllerHasRoles(typeof(StatsController), UserRole.Admin, UserRole.Coach);
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
		AssertMethodHasRoles(typeof(FilesController), "CreateUploadUrl", UserRole.Admin, UserRole.Coach);
		AssertMethodHasRoles(typeof(FilesController), "MarkUploaded", UserRole.Admin, UserRole.Coach);
		AssertMethodHasRoles(typeof(FilesController), "Delete", UserRole.Admin, UserRole.Coach);
	}

	[Test]
	public void SeasonsController_ShouldRequireAuthorization()
	{
		var authorizeAttributes = GetAuthorizeAttributes(typeof(SeasonsController));

		Assert.That(authorizeAttributes, Is.Not.Empty, "SeasonsController should require authorization.");
	}

	[Test]
	public void SeasonsController_WriteActions_ShouldBeAdminOnly()
	{
		AssertMethodHasRoles(typeof(SeasonsController), "Create", UserRole.Admin);
		AssertMethodHasRoles(typeof(SeasonsController), "Update", UserRole.Admin);
		AssertMethodHasRoles(typeof(SeasonsController), "SetActive", UserRole.Admin);
		AssertMethodHasRoles(typeof(SeasonsController), "SetupSeason", UserRole.Admin);
	}

	private static void AssertControllerHasRoles(Type controllerType, params UserRole[] expectedRoles)
	{
		var authorizeAttributes = GetAuthorizeAttributes(controllerType);

		Assert.That(authorizeAttributes, Is.Not.Empty, $"{controllerType.Name} should have an Authorize attribute.");

		var actualRoles = GetRoles(authorizeAttributes);
		var expectedRoleNames = expectedRoles.Select(role => role.ToString()).OrderBy(role => role).ToArray();

		Assert.That(actualRoles, Is.EqualTo(expectedRoleNames), $"{controllerType.Name} should only allow the expected roles.");
	}

	private static void AssertMethodHasRoles(Type controllerType, string methodName, params UserRole[] expectedRoles)
	{
		var method = GetMethod(controllerType, methodName);
		var authorizeAttributes = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray();

		Assert.That(authorizeAttributes, Is.Not.Empty, $"{controllerType.Name}.{methodName} should have an Authorize attribute.");

		var actualRoles = GetRoles(authorizeAttributes);
		var expectedRoleNames = expectedRoles.Select(role => role.ToString()).OrderBy(role => role).ToArray();

		Assert.That(actualRoles, Is.EqualTo(expectedRoleNames), $"{controllerType.Name}.{methodName} should only allow the expected roles.");
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
