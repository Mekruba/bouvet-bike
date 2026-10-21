using BouvetBike.Shared.Domain;

namespace BouvetBike.Orders.Domain;

/// <summary>The requested due date has no capacity left. Tells the client the next date that has.</summary>
public sealed class DueDateFullException : DomainException
{
    public DateOnly NextAvailableDate { get; }

    public DueDateFullException(DateOnly requested, DateOnly nextAvailable)
        : base($"{requested:yyyy-MM-dd} is fully booked. The next available date is {nextAvailable:yyyy-MM-dd}.")
    {
        NextAvailableDate = nextAvailable;
        Details = new Dictionary<string, object?> { ["nextAvailableDate"] = nextAvailable };
    }
}
