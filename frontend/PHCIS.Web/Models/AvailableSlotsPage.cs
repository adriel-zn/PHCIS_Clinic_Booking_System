namespace PHCIS.Web.Models;

public sealed record AvailableSlotsPage(IReadOnlyList<SlotSummary> Items, int Offset, int Limit, bool HasMore);

