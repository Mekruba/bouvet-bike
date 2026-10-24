using BouvetBike.Shared.Domain;
using NetArchTest.Rules;

namespace BouvetBike.Tests;

/// <summary>
/// Single project, so module and layer boundaries are enforced by namespace instead of project references.
/// Each module: Api, Contracts (DTOs + service interfaces: the module's public surface), Application (implementations),
/// Domain, Persistence. Shared is the host: it may wire modules together (AppDbContext, seeding).
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
    public void Contracts_AreFreeOfApplicationPersistenceAndFrameworks() =>
        AssertNoDependency(
            AllTypes.ResideInNamespaceMatching(@"^BouvetBike\.\w+\.Contracts"),
            [
                "Microsoft.EntityFrameworkCore",
                // JSON Patch (Microsoft.AspNetCore.JsonPatch) is allowed: the patch document is part of the API contract.
                "Microsoft.AspNetCore.Http",
                "Microsoft.AspNetCore.Routing",
                "Microsoft.AspNetCore.Mvc",
                "Microsoft.AspNetCore.Identity",
                .. Modules.SelectMany(module => new[] { "Api", "Application", "Persistence" }.Select(layer => $"BouvetBike.{module}.{layer}")),
            ]);

    [Fact]
    public void Application_DoesNotDependOnApiOrHttp() =>
        AssertNoDependency(
            AllTypes.ResideInNamespaceMatching(@"^BouvetBike\.\w+\.Application"),
            [
                "Microsoft.AspNetCore.Http",
                .. Modules.Select(module => $"BouvetBike.{module}.Api"),
            ]);

    /// <summary>A module may use another module's Contracts and Domain, never its implementation.</summary>
    [Theory]
    [InlineData("Orders")]
    [InlineData("Catalog")]
    [InlineData("Discounts")]
    [InlineData("Users")]
    public void Modules_UseOnlyContractsAndDomainOfOtherModules(string module) =>
        AssertNoDependency(
            AllTypes.ResideInNamespace($"BouvetBike.{module}"),
            Modules
                .Where(other => other != module && other != "Shared")
                .SelectMany(other => new[] { "Api", "Application", "Persistence" }.Select(layer => $"BouvetBike.{other}.{layer}"))
                .ToArray());

    [Fact]
    public void SharedDomain_DoesNotDependOnAnyModule() =>
        AssertNoDependency(
            AllTypes.ResideInNamespace("BouvetBike.Shared.Domain"),
            Modules.Where(m => m != "Shared").Select(m => $"BouvetBike.{m}").ToArray());

    [Theory]
    [InlineData("Catalog")]
    [InlineData("Discounts")]
    [InlineData("Users")] // Orders depends on Users, so Users reaches Orders only through its own contracts (ICustomerAccountEvents)
    public void UpstreamModules_DoNotDependOnOrders(string module) =>
        AssertNoDependency(AllTypes.ResideInNamespace($"BouvetBike.{module}"), "BouvetBike.Orders");

    private static void AssertNoDependency(PredicateList types, params string[] namespaces)
    {
        var result = types.ShouldNot().HaveDependencyOnAny(namespaces).GetResult();
        Assert.True(result.IsSuccessful, $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
