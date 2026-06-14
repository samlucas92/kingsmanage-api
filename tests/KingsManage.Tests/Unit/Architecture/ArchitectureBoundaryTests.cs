using System.Xml.Linq;
using NUnit.Framework;

namespace KingsManage.Tests.Unit.Architecture;

[TestFixture]
public class ArchitectureBoundaryTests
{
	private static readonly string RepositoryRoot = GetRepositoryRoot();

	[Test]
	public void ProjectReferences_ShouldFollowOneWayDependencyDirection()
	{
		AssertProjectReferences(
			"src/KingsManage/KingsManage.csproj"
		);

		AssertProjectReferences(
			"src/KingsManage.Mongo/KingsManage.Mongo.csproj",
			"KingsManage.csproj"
		);

		AssertProjectReferences(
			"src/KingsManage.Web/KingsManage.Web.csproj",
			"KingsManage.csproj",
			"KingsManage.Mongo.csproj"
		);
	}

	[Test]
	public void CoreProject_ShouldNotUseWebOrMongoNamespaces()
	{
		var violations = FindSourceFiles("src/KingsManage")
			.Where(file => FileContainsAny(file,
				"KingsManage.Web",
				"KingsManage.Mongo"))
			.ToArray();

		Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
	}

	[Test]
	public void MongoProject_ShouldNotUseWebNamespaces()
	{
		var violations = FindSourceFiles("src/KingsManage.Mongo")
			.Where(file => FileContainsAny(file,
				"KingsManage.Web"))
			.ToArray();

		Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
	}

	[Test]
	public void ControllerNamespace_ShouldOnlyBeUsedInsideControllersFolder()
	{
		var violations = FindSourceFiles("src")
			.Where(file => FileContains(file, "namespace KingsManage.Web.Controllers"))
			.Where(file => !NormalisePath(file).Contains("/KingsManage.Web/Controllers/"))
			.ToArray();

		Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
	}

	[Test]
	public void WebModelsNamespace_ShouldOnlyBeUsedInsideWebModelsFolder()
	{
		var violations = FindSourceFiles("src")
			.Where(file => FileContains(file, "namespace KingsManage.Web.Models"))
			.Where(file => !NormalisePath(file).Contains("/KingsManage.Web/Models/"))
			.ToArray();

		Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
	}

	[Test]
	public void CoreAndMongo_ShouldNotReferenceWebModels()
	{
		var violations = FindSourceFiles("src/KingsManage")
			.Concat(FindSourceFiles("src/KingsManage.Mongo"))
			.Where(file => FileContainsAny(file,
				"KingsManage.Web.Models",
				"using KingsManage.Web"))
			.ToArray();

		Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
	}

	private static void AssertProjectReferences(string projectFilePath, params string[] expectedReferencedProjectFileNames)
	{
		var fullPath = Path.Combine(RepositoryRoot, projectFilePath);
		Assert.That(File.Exists(fullPath), Is.True, $"Could not find project file: {fullPath}");

		var document = XDocument.Load(fullPath);

		var actualReferences = document
			.Descendants("ProjectReference")
			.Select(element => element.Attribute("Include")?.Value)
			.Where(include => !string.IsNullOrWhiteSpace(include))
			.Select(NormaliseProjectReference)
			.OrderBy(fileName => fileName)
			.ToArray();

		var expectedReferences = expectedReferencedProjectFileNames
			.Select(NormaliseProjectReference)
			.OrderBy(fileName => fileName)
			.ToArray();

		Assert.That(actualReferences, Is.EqualTo(expectedReferences), projectFilePath);
	}

	private static string NormaliseProjectReference(string? reference)
	{
		if (string.IsNullOrWhiteSpace(reference))
		{
			return string.Empty;
		}

		var normalisedReference = reference
			.Replace('\\', '/')
			.Trim();

		var segments = normalisedReference
			.Split('/', StringSplitOptions.RemoveEmptyEntries);

		return segments.LastOrDefault() ?? normalisedReference;
	}

	private static IEnumerable<string> FindSourceFiles(string relativePath)
	{
		var fullPath = Path.Combine(RepositoryRoot, relativePath);

		if (!Directory.Exists(fullPath))
		{
			return Array.Empty<string>();
		}

		return Directory
			.EnumerateFiles(fullPath, "*.cs", SearchOption.AllDirectories)
			.Where(file => !NormalisePath(file).Contains("/bin/"))
			.Where(file => !NormalisePath(file).Contains("/obj/"));
	}

	private static bool FileContains(string filePath, string value)
	{
		return File.ReadAllText(filePath).Contains(value, StringComparison.Ordinal);
	}

	private static bool FileContainsAny(string filePath, params string[] values)
	{
		var text = File.ReadAllText(filePath);

		return values.Any(value => text.Contains(value, StringComparison.Ordinal));
	}

	private static string NormalisePath(string path)
	{
		return path.Replace('\\', '/');
	}

	private static string GetRepositoryRoot()
	{
		var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

		while (directory != null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, "src")) && Directory.Exists(Path.Combine(directory.FullName, "tests")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException("Could not find repository root from test directory.");
	}
}
