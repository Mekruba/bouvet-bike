using BouvetBike.Discounts.Contracts;
using BouvetBike.Discounts.Domain;
using BouvetBike.Shared.Domain;
using BouvetBike.Shared.Persistence;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.EntityFrameworkCore;

namespace BouvetBike.Discounts.Application;

public sealed class DiscountService(AppDbContext db, TimeProvider clock) : IDiscountService
{
    public async Task<AppliedDiscount> PreviewAsync(string code, CancellationToken ct) =>
        (await FindAsync(code, ct)).Preview(clock);

    public async Task<AppliedDiscount> RedeemAsync(string code, CancellationToken ct) =>
        (await FindAsync(code, ct)).Redeem(clock); // tracked: saved with the caller's SaveChanges

    public Task<IReadOnlyList<DiscountCodeResponse>> GetAllAsync(CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public Task<DiscountCodeResponse> GetByIdAsync(Guid id, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public Task<DiscountCodeResponse> CreateAsync(CreateDiscountCodeRequest request, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public Task<DiscountCodeResponse> PatchAsync(Guid id, JsonPatchDocument<DiscountCodePatch> patch, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    private async Task<DiscountCode> FindAsync(string code, CancellationToken ct)
    {
        var normalized = DiscountCode.Normalize(code);
        return await db.DiscountCodes.SingleOrDefaultAsync(d => d.Code == normalized, ct)
            ?? throw new DomainException($"Discount code '{normalized}' is not valid.");
    }
}
