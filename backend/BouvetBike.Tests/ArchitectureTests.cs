using BouvetBike.Domain;
using NetArchTest.Rules;

namespace BouvetBike.Tests;

/// <summary>Single project, so layer boundaries are enforced by namespace instead of project references.</summary>
public class ArchitectureTests
{
    [Fact]
    public void Domain_DoesNotDependOnInfrastructureOrWeb()
    {
        var result = Types.InAssembly(typeof(DomainException).Assembly)
            .That().ResideInNamespace("BouvetBike.Domain")
            .ShouldNot().HaveDependencyOnAny(
                "BouvetBike.Infrastructure",
                "BouvetBike.Endpoints",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
