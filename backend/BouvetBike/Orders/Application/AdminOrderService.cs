using BouvetBike.Catalog.Contracts;
using BouvetBike.Discounts.Contracts;
using BouvetBike.Discounts.Domain;
using BouvetBike.Orders.Contracts;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Application;
using BouvetBike.Shared.Contracts;
using BouvetBike.Shared.Domain;
using BouvetBike.Shared.Persistence;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.EntityFrameworkCore;

namespace BouvetBike.Orders.Application;

public sealed class AdminOrderService(
    AppDbContext db,
    ICatalogService catalog,
    IDiscountService discounts,
    ICapacityService capacity,
    TimeProvider clock) : IAdminOrderService
{
    public async Task<PagedResponse<AdminOrderSummaryResponse>> SearchAsync(AdminOrderQuery query, CancellationToken ct)
    {
        var orders = db.MaintenanceOrders.IgnoreAutoIncludes().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            orders = ApplySearch(orders, query.Search.Trim());
        }

        if (query.Status is { } status)
        {
            orders = orders.Where(o => o.Status == status);
        }

        if (query.ServiceTypeId is { } serviceTypeId)
        {
            orders = orders.Where(o => o.ServiceTypeId == serviceTypeId);
        }

        if (query.DueFrom is { } from)
        {
            orders = orders.Where(o => o.DueDate >= from);
        }

        if (query.DueTo is { } to)
        {
            orders = orders.Where(o => o.DueDate <= to);
        }

        var totalCount = await orders.CountAsync(ct);
        var page = await Sort(orders, query.Sort, query.Descending)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var items = page.Select(o => new AdminOrderSummaryResponse(
                o.Id, o.Reference.Value, o.Status, o.Contact.ToContactResponse(), IsGuest: o.CustomerId is null,
                o.BikeBrand, o.ServiceName, o.DueDate, MoneyResponse.From(o.Total), o.CreatedAt))
            .ToList();
        return new PagedResponse<AdminOrderSummaryResponse>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<AdminOrderResponse> GetAsync(Guid id, CancellationToken ct) =>
        await ToResponseAsync(await LoadAsync(id, ct), ct);

    public async Task<AdminOrderResponse> PatchAsync(Guid id, JsonPatchDocument<AdminOrderPatch> patch, CancellationToken ct)
    {
        var order = await LoadAsync(id, ct);
        var original = ToPatchModel(order);
        var model = ToPatchModel(order);
        patch.ApplyAndValidate(model);

        // Apply only what changed, so e.g. internal notes stay editable on a completed order while details don't.
        // Nothing is saved unless every step succeeds.
        try
        {
            if (DetailsChanged(original, model))
            {
                if (model.DueDate != order.DueDate)
                {
                    await capacity.EnsureAvailableAsync(model.DueDate, ignoreOrderId: order.Id, ct);
                }

                var contact = new ContactInfo(model.Name, Email.Create(model.Email), PhoneNumber.Create(model.Phone));
                order.UpdateDetails(contact, model.BikeBrand, model.BikeModel, model.DueDate, clock);
            }

            if (model.ServiceTypeId != original.ServiceTypeId)
            {
                order.ChangeService(await catalog.GetOrderableServiceAsync(model.ServiceTypeId, ct), clock);
            }

            if (model.InternalNotes != original.InternalNotes)
            {
                order.UpdateInternalNotes(model.InternalNotes, clock);
            }

            var newCode = NormalizeCode(model.DiscountCode);
            if (newCode != order.Discount?.Code)
            {
                if (order.Discount is not null)
                {
                    order.RemoveDiscount(clock);
                }

                if (newCode is not null)
                {
                    order.ApplyDiscount(await discounts.RedeemAsync(newCode, ct), clock);
                }
            }
        }
        catch (DomainException ex)
        {
            // RFC 5789: a patch that would break a business rule is 422, not 400.
            throw UnprocessableException.From(ex);
        }

        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(order, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        db.MaintenanceOrders.Remove(await LoadAsync(id, ct));
        await db.SaveChangesAsync(ct);
    }

    public Task<StatusChangeResponse> AddStatusChangeAsync(Guid id, ChangeStatusRequest request, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public Task<IReadOnlyList<StatusChangeResponse>> GetStatusChangesAsync(Guid id, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public Task<StatusChangeResponse> GetStatusChangeAsync(Guid id, Guid changeId, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public Task<PriceOverrideResponse> GetPriceOverrideAsync(Guid id, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public Task<(PriceOverrideResponse Override, bool Created)> SetPriceOverrideAsync(Guid id, OverridePriceRequest request, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public Task ClearPriceOverrideAsync(Guid id, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    /// <summary>
    /// Reference, email and phone match exactly (they're value objects, stored whole); name and bike brand
    /// match as a case-insensitive substring. The term is parsed in each form; whatever parses is searched.
    /// </summary>
    private static IQueryable<MaintenanceOrder> ApplySearch(IQueryable<MaintenanceOrder> orders, string term)
    {
        var pattern = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        OrderReference.TryParse(term, out var reference);
        var email = TryParse(() => Email.Create(term));
        var phone = TryParse(() => PhoneNumber.Create(term));

        return orders.Where(o =>
            EF.Functions.Like(o.Contact.Name, pattern, "\\")
            || EF.Functions.Like(o.BikeBrand, pattern, "\\")
            || (reference != null && o.Reference == reference)
            || (email != null && o.Contact.Email == email)
            || (phone != null && o.Contact.Phone == phone));
    }

    private static IQueryable<MaintenanceOrder> Sort(IQueryable<MaintenanceOrder> orders, OrderSort sort, bool descending)
    {
        var sorted = (sort, descending) switch
        {
            (OrderSort.CreatedAt, false) => orders.OrderBy(o => o.CreatedAt),
            (OrderSort.CreatedAt, true) => orders.OrderByDescending(o => o.CreatedAt),
            (OrderSort.CustomerName, false) => orders.OrderBy(o => o.Contact.Name),
            (OrderSort.CustomerName, true) => orders.OrderByDescending(o => o.Contact.Name),
            (OrderSort.Status, false) => orders.OrderBy(o => o.Status),
            (OrderSort.Status, true) => orders.OrderByDescending(o => o.Status),
            (_, false) => orders.OrderBy(o => o.DueDate),
            (_, true) => orders.OrderByDescending(o => o.DueDate),
        };

        return sorted.ThenBy(o => o.CreatedAt).ThenBy(o => o.Id); // stable pages
    }

    private async Task<MaintenanceOrder> LoadAsync(Guid id, CancellationToken ct) =>
        await db.MaintenanceOrders.SingleOrDefaultAsync(o => o.Id == id, ct)
        ?? throw new NotFoundException($"Order {id} was not found.");

    private static AdminOrderPatch ToPatchModel(MaintenanceOrder order) => new()
    {
        Name = order.Contact.Name,
        Email = order.Contact.Email.Value,
        Phone = order.Contact.Phone.Value,
        BikeBrand = order.BikeBrand,
        BikeModel = order.BikeModel,
        ServiceTypeId = order.ServiceTypeId,
        DueDate = order.DueDate,
        InternalNotes = order.InternalNotes,
        DiscountCode = order.Discount?.Code,
    };

    private static bool DetailsChanged(AdminOrderPatch before, AdminOrderPatch after) =>
        before.Name != after.Name
        || before.Email != after.Email
        || before.Phone != after.Phone
        || before.BikeBrand != after.BikeBrand
        || before.BikeModel != after.BikeModel
        || before.DueDate != after.DueDate;

    private static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : DiscountCode.Normalize(code);

    private static T? TryParse<T>(Func<T> parse) where T : class
    {
        try
        {
            return parse();
        }
        catch (DomainException)
        {
            return null;
        }
    }

    private async Task<AdminOrderResponse> ToResponseAsync(MaintenanceOrder order, CancellationToken ct)
    {
        var userIds = order.StatusHistory.Select(c => c.ChangedById)
            .Append(order.PriceOverride?.OverriddenById)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        string? NameOf(Guid? id) => id is { } userId ? names.GetValueOrDefault(userId) : null;

        return new AdminOrderResponse(
            order.Id,
            order.Reference.Value,
            order.Status,
            OrderStatusTransitions.From(order.Status),
            order.Contact.ToContactResponse(),
            order.CustomerId,
            order.BikeBrand,
            order.BikeModel,
            order.ServiceTypeId,
            order.ServiceName,
            order.DueDate,
            order.CustomerNotes,
            order.InternalNotes,
            order.ToPriceResponse(),
            order.PriceOverride is { } priceOverride
                ? new PriceOverrideResponse(
                    MoneyResponse.From(priceOverride.Amount), priceOverride.Reason, NameOf(priceOverride.OverriddenById), priceOverride.OverriddenAt)
                : null,
            order.StatusHistory
                .OrderBy(c => c.ChangedAt)
                .Select(c => new StatusChangeResponse(c.Id, c.From, c.To, NameOf(c.ChangedById), c.ChangedAt, c.Reason))
                .ToList(),
            order.CreatedAt,
            order.UpdatedAt);
    }
}
