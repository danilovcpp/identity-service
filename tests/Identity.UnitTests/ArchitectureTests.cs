using System.Xml.Linq;

namespace Identity.UnitTests;

public sealed class ArchitectureTests
{
    [Theory]
    [InlineData("Identity.Domain", new string[0])]
    [InlineData("Identity.Application", new[] { "Identity.Domain" })]
    [InlineData("Identity.Infrastructure", new[] { "Identity.Application", "Identity.Domain" })]
    [InlineData("Identity.Api", new[] { "Identity.Application", "Identity.Infrastructure" })]
    public void ProjectReferencesFollowCleanArchitecture(string projectName, string[] expected)
    {
        var project = LoadProject(projectName);
        var references = project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension((string)reference.Attribute("Include")!))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, references);
    }

    [Fact]
    public void DomainHasNoPackageOrFrameworkDependencies()
    {
        var project = LoadProject("Identity.Domain");

        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("FrameworkReference"));
        Assert.Equal("Microsoft.NET.Sdk", (string?)project.Root?.Attribute("Sdk"));
    }

    [Fact]
    public void ApplicationOnlyUsesDependencyInjectionAbstractions()
    {
        var project = LoadProject("Identity.Application");
        var package = Assert.Single(project.Descendants("PackageReference"));

        Assert.Equal("Microsoft.Extensions.DependencyInjection.Abstractions", (string?)package.Attribute("Include"));
        Assert.Empty(project.Descendants("FrameworkReference"));
        Assert.Equal("Microsoft.NET.Sdk", (string?)project.Root?.Attribute("Sdk"));
    }

    private static XDocument LoadProject(string projectName) =>
        XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Architecture", projectName, $"{projectName}.csproj"));
}
