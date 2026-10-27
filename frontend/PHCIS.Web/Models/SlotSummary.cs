namespace PHCIS.Web.Models;

public sealed record SlotSummary(Guid Id, Guid ClinicId, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

