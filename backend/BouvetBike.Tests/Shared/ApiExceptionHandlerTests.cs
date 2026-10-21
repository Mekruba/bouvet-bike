using System.Text.Json;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Api;
using BouvetBike.Shared.Application;
using BouvetBike.Shared.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BouvetBike.Tests.Shared;

public class ApiExceptionHandlerTests
{
    [Fact]
    public async Task NotFound_Becomes404()
    {
        var (status, body) = await Handle(new NotFoundException("Order not found."));

        Assert.Equal(404, status);
        Assert.Equal("Order not found.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task DomainException_Becomes400_WithDetailsAsExtraFields()
    {
        var (status, body) = await Handle(new DueDateFullException(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 4)));

        Assert.Equal(400, status);
        Assert.Equal("2026-03-04", body.GetProperty("nextAvailableDate").GetString());
    }

    [Fact]
    public async Task Conflict_Becomes409()
    {
        var (status, _) = await Handle(new ConflictException("Name taken."));

        Assert.Equal(409, status);
    }

    [Fact]
    public async Task Unprocessable_Becomes422_WithFieldErrors()
    {
        var errors = new Dictionary<string, string[]> { ["price"] = ["Must be positive."] };

        var (status, body) = await Handle(new UnprocessableException("Invalid patch.", errors));

        Assert.Equal(422, status);
        Assert.Equal("Must be positive.", body.GetProperty("errors").GetProperty("price")[0].GetString());
    }

    [Fact]
    public async Task OtherExceptions_AreNotHandled()
    {
        var handled = await new ApiExceptionHandler(ProblemDetailsService())
            .TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException(), CancellationToken.None);

        Assert.False(handled);
    }

    private static async Task<(int Status, JsonElement Body)> Handle(Exception exception)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, Response = { Body = new MemoryStream() } };

        var handled = await new ApiExceptionHandler(ProblemDetailsService(services))
            .TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body));
    }

    private static IProblemDetailsService ProblemDetailsService(IServiceProvider? services = null) =>
        (services ?? new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider())
            .GetRequiredService<IProblemDetailsService>();
}
