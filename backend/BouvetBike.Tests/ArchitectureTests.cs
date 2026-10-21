using BouvetBike.Shared.Domain;
using NetArchTest.Rules;

namespace BouvetBike.Tests;

/// <summary>
/// Single project, so module and layer boundaries are enforced by namespace instead of project references.
/// Each module: Api, Contracts, Application, Domain, Persistence.
/// </summary>
public class ArchitectureTests
{
    private static readonly string[] Modules = ["Orders", "Catalog", "Discounts", "Users", "Shared"];
    private static readonly string[] OuterLayers = ["Api", "Contracts", "Application", "Persistence"];

    private static Predicates AllTypes => Types.InAssembly(typeof(DomainException).Assembly).That();

    [Fact]
    public void Domain_DependsOnlyOnOtherDomainCode()
    {
        string[] forbidden =
        [
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "BouvetBike.Users",
            .. Modules.SelectMany(module => OuterLayers.Select(layer => $"BouvetBike.{module}.{layer}")),
        ];

        AssertNoDependency(AllTypes.ResideInNamespaceMatching(@"^BouvetBike\.\w+\.Domain"), forbidden);
    }

    [Fact]
    public void SharedDomain_DoesNotDependOnAnyModule() =>
        AssertNoDependency(
            AllTypes.ResideInNamespace("BouvetBike.Shared.Domain"),
            Modules.Where(m => m != "Shared").Select(m => $"BouvetBike.{m}").ToArray());

    [Theory]
    [InlineData("Catalog")]
    [InlineData("Discounts")]
    public void UpstreamModules_DoNotDependOnOrders(string module) =>
        AssertNoDependency(AllTypes.ResideInNamespace($"BouvetBike.{module}"), "BouvetBike.Orders");

    private static void AssertNoDependency(PredicateList types, params string[] namespaces)
    {
        var result = types.ShouldNot().HaveDependencyOnAny(namespaces).GetResult();
        Assert.True(result.IsSuccessful, $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
