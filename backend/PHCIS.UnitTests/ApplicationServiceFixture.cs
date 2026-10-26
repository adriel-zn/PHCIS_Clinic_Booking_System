using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Application;
using PHCIS.Application.Abstractions;
using PHCIS.Application.Services;
using PHCIS.Application.Validation;
using PHCIS.Domain.Entities;

namespace PHCIS.UnitTests;

internal sealed class ApplicationServiceFixture : IDisposable
{
    private readonly ServiceProvider _provider;
    public FakeAppointmentStore Store { get; } = new();
    public IMapper Mapper { get; }
    public TimeProvider Clock { get; set; } = new TestClock(DomainTestData.Now);

    public ApplicationServiceFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(new ConfigurationBuilder().Build());
        _provider = services.BuildServiceProvider();
        Mapper = _provider.GetRequiredService<IMapper>();
    }

    public AppointmentService Appointments() => new(
        Store, Store, Store, Store,
        new BookAppointmentRequestValidator(),
        new CancelAppointmentRequestValidator(),
        new RescheduleAppointmentRequestValidator(),
        Mapper, Clock);

    public AvailabilityService Availability() => new(
        Store, new AvailableSlotsRequestValidator(), Mapper, Clock);

    public Appointment SeedAppointment()
    {
        var appointment = DomainTestData.Appointment();
        Store.Patients.Add(appointment.Patient);
        Store.Slots.Add(appointment.TimeSlot);
        Store.Appointments.Add(appointment);
        return appointment;
    }

    public void Dispose() => _provider.Dispose();
}

internal sealed class FakeAppointmentStore :
    IPatientReader, ITimeSlotRepository, IAppointmentRepository, IUnitOfWork
{
    public List<Patient> Patients { get; } = [];
    public List<TimeSlot> Slots { get; } = [];
    public List<Appointment> Appointments { get; } = [];
    public List<TimeSlot> AvailabilityResults { get; } = [];
    public bool ClinicExists { get; set; } = true;
    public int Reads { get; private set; }
    public int Saves { get; private set; }
    public Appointment? Pending { get; private set; }
    public Exception? SaveFailure { get; set; }
    public CancellationToken LastToken { get; private set; }
    public (DateTimeOffset From, DateTimeOffset To, DateTimeOffset Now, int Offset, int Take) LastSearch { get; private set; }

    private void Read(CancellationToken cancellationToken)
    {
        Reads++;
        LastToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
    }

    Task<Patient?> IPatientReader.FindAsync(Guid patientId, CancellationToken cancellationToken)
    {
        Read(cancellationToken);
        return Task.FromResult(Patients.SingleOrDefault(x => x.Id == patientId));
    }

    Task<TimeSlot?> ITimeSlotRepository.FindAsync(Guid timeSlotId, CancellationToken cancellationToken)
    {
        Read(cancellationToken);
        return Task.FromResult(Slots.SingleOrDefault(x => x.Id == timeSlotId));
    }

    public Task<bool> ClinicExistsAsync(Guid clinicId, CancellationToken cancellationToken)
    {
        Read(cancellationToken);
        return Task.FromResult(ClinicExists);
    }

    public Task<IReadOnlyList<TimeSlot>> FindAvailableAsync(
        Guid clinicId, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now,
        int offset, int take, CancellationToken cancellationToken)
    {
        Read(cancellationToken);
        LastSearch = (from, to, now, offset, take);
        return Task.FromResult<IReadOnlyList<TimeSlot>>(AvailabilityResults);
    }

    public Task<Appointment?> FindWithSlotAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        Read(cancellationToken);
        return Task.FromResult(Appointments.SingleOrDefault(x => x.Id == appointmentId));
    }

    public Task<bool> IsSlotOccupiedAsync(Guid timeSlotId, CancellationToken cancellationToken)
    {
        Read(cancellationToken);
        return Task.FromResult(Appointments.Any(x =>
            x.TimeSlotId == timeSlotId && x.Status != AppointmentStatus.Cancelled));
    }

    public void Add(Appointment appointment) => Pending = appointment;

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        LastToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        Saves++;

        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

        if (Pending is not null)
        {
            Appointments.Add(Pending);
            Pending = null;
        }

        return Task.CompletedTask;
    }
}

