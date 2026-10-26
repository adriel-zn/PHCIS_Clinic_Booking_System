using PHCIS.Domain.Exceptions;

namespace PHCIS.Domain.Entities;

public sealed class TimeSlot
{
    private readonly List<Appointment> _appointments = [];
    private byte[] _rowVersion = [];

    private TimeSlot() { }

    public TimeSlot(Clinic clinic, DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        ArgumentNullException.ThrowIfNull(clinic);

        if (startsAt == default || endsAt <= startsAt)
        {
            throw new DomainRuleException("A time slot must have a specified start and end after its start.");
        }

        Id = Guid.NewGuid();
        Clinic = clinic;
        ClinicId = clinic.Id;
        StartsAt = startsAt.ToUniversalTime();
        EndsAt = endsAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Clinic Clinic { get; private set; } = null!;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public byte[] RowVersion => (byte[])_rowVersion.Clone();
    public IReadOnlyCollection<Appointment> Appointments => _appointments.AsReadOnly();
}

