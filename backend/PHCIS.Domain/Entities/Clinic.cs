using PHCIS.Domain.Common;

namespace PHCIS.Domain.Entities;

public sealed class Clinic
{
    private readonly List<TimeSlot> _timeSlots = [];
    private byte[] _rowVersion = [];

    private Clinic() { }

    public Clinic(string name, string address, string? phoneNumber = null)
    {
        Id = Guid.NewGuid();
        UpdateDetails(name, address, phoneNumber);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public byte[] RowVersion => (byte[])_rowVersion.Clone();
    public IReadOnlyCollection<TimeSlot> TimeSlots => _timeSlots.AsReadOnly();

    public void UpdateDetails(string name, string address, string? phoneNumber = null)
    {
        var normalizedName = DomainGuard.RequiredText(name, 200, "Clinic name");
        var normalizedAddress = DomainGuard.RequiredText(address, 500, "Address");
        var normalizedPhone = DomainGuard.OptionalText(phoneNumber, 30, "Phone number");

        Name = normalizedName;
        Address = normalizedAddress;
        PhoneNumber = normalizedPhone;
    }
}

