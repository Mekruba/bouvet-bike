using BouvetBike.Discounts.Application;
using BouvetBike.Discounts.Contracts;

namespace BouvetBike.Discounts.Api;

public static class DiscountsModule
{
    /// <summary>No endpoints yet: discount codes are used through orders.</summary>
    public static IServiceCollection AddDiscountsModule(this IServiceCollection services) =>
        services.AddScoped<IDiscountService, DiscountService>();
}
