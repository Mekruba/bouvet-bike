using BouvetBike.Discounts.Contracts;
using BouvetBike.Discounts.Domain;
using BouvetBike.Shared.Application;
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

    public async Task<IReadOnlyList<DiscountCodeResponse>> GetAllAsync(CancellationToken ct) =>
        (await db.DiscountCodes.OrderBy(d => d.Code).ToListAsync(ct)).Select(ToResponse).ToList();

    public async Task<DiscountCodeResponse> GetByIdAsync(Guid id, CancellationToken ct) =>
        ToResponse(await LoadAsync(id, ct));

    public async Task<DiscountCodeResponse> CreateAsync(CreateDiscountCodeRequest request, CancellationToken ct)
    {
        var normalized = DiscountCode.Normalize(request.Code);
        if (await db.DiscountCodes.AnyAsync(d => d.Code == normalized, ct))
        {
            throw new ConflictException($"The discount code '{normalized}' already exists.");
        }

        var code = DiscountCode.Create(
            request.Code, request.Type!.Value, request.Value!.Value, request.ValidFrom, request.ValidTo, request.MaxUses);
        db.DiscountCodes.Add(code);
        await db.SaveChangesAsync(ct);
        return ToResponse(code);
    }

    public async Task<DiscountCodeResponse> PatchAsync(Guid id, JsonPatchDocument<DiscountCodePatch> patch, CancellationToken ct)
    {
        var code = await LoadAsync(id, ct);
        var model = new DiscountCodePatch
        {
            Type = code.Type,
            Value = code.Value,
            ValidFrom = code.ValidFrom,
            ValidTo = code.ValidTo,
            MaxUses = code.MaxUses,
            IsActive = code.IsActive,
        };

        patch.ApplyAndValidate(model);
        try
        {
            code.UpdateTerms(model.Type, model.Value, model.ValidFrom, model.ValidTo, model.MaxUses);
        }
        catch (DomainException ex)
        {
            // RFC 5789: a patch that would leave the resource invalid is 422, not 400.
            throw UnprocessableException.From(ex);
        }

        if (model.IsActive)
        {
            code.Activate();
        }
        else
        {
            code.Deactivate();
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(code);
    }

    private async Task<DiscountCode> LoadAsync(Guid id, CancellationToken ct) =>
        await db.DiscountCodes.FindAsync([id], ct) ?? throw new NotFoundException($"Discount code {id} was not found.");

    private static DiscountCodeResponse ToResponse(DiscountCode code) => new(
        code.Id, code.Code, code.Type, code.Value, code.ValidFrom, code.ValidTo, code.MaxUses, code.TimesUsed, code.IsActive);

    private async Task<DiscountCode> FindAsync(string code, CancellationToken ct)
    {
        var normalized = DiscountCode.Normalize(code);
        return await db.DiscountCodes.SingleOrDefaultAsync(d => d.Code == normalized, ct)
            ?? throw new DomainException($"Discount code '{normalized}' is not valid.");
    }
}
