using PHCIS.Domain.Entities;

namespace PHCIS.UnitTests;

internal static class DomainTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 26, 10, 0, 0, TimeSpan.Zero);
    public static Patient Patient() => new("Test", "Patient", new DateOnly(2000, 1, 1), new TestClock(Now));
    public static Clinic Clinic() => new("Test clinic", "1 Main Road");
    public static TimeSlot Slot() => new(Clinic(), Now.AddHours(1), Now.AddHours(2));
    public static Appointment Appointment() => new(Patient(), Slot(), new TestClock(Now));
}

internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

