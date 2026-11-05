using System.Xml.Linq;
using Shouldly;

namespace TMPublisher.Tests.Architecture;

/// <summary>
/// Layer boundaries: Core references no project of ours; Platforms, Storage, Scheduling and Runtime reference Core only; only the two
/// composition roots (the window program and the background scheduler) reference them. A new reference across the boundary fails here.
/// </summary>
public sealed class LayerTests
{
    public static TheoryData<string, string[]> Allowed() => new()
    {
        { "TMPublisher.Core", [] },
        { "TMPublisher.Platforms", ["TMPublisher.Core"] },
        { "TMPublisher.Storage", ["TMPublisher.Core"] },
        { "TMPublisher.Scheduling", ["TMPublisher.Core"] },
        { "TMPublisher.Runtime", ["TMPublisher.Core"] },
        { "TMPublisher.Desktop", ["TMPublisher.Core", "TMPublisher.Platforms", "TMPublisher.Storage", "TMPublisher.Scheduling", "TMPublisher.Runtime"] },
        { "TMPublisher.Background", ["TMPublisher.Core", "TMPublisher.Platforms", "TMPublisher.Storage", "TMPublisher.Scheduling", "TMPublisher.Runtime"] },
    };

    [Theory]
    [MemberData(nameof(Allowed))]
    public void ProjectReferencesStayInsideTheLayer(string project, string[] allowed)
    {
        var path = Path.Combine(RepositoryFiles.Root, "src", project, project + ".csproj");
        var references = XDocument.Load(path).Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/')))
            .Order(StringComparer.Ordinal)
            .ToArray();
        references.ShouldBe(allowed.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void CoreHasNoUiStorageOrHttpFrameworkPackage()
    {
        var path = Path.Combine(RepositoryFiles.Root, "src", "TMPublisher.Core", "TMPublisher.Core.csproj");
        var packages = XDocument.Load(path).Descendants("PackageReference").Select(e => e.Attribute("Include")!.Value).ToArray();
        packages.ShouldBe(["Microsoft.Extensions.Logging.Abstractions"]);
    }
}
