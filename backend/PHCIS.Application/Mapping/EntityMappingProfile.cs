using AutoMapper;
using PHCIS.Application.Models;
using PHCIS.Domain.Entities;

namespace PHCIS.Application.Mapping;

public sealed class EntityMappingProfile : Profile
{
    public EntityMappingProfile()
    {
        CreateMap<Patient, PatientDto>();
        CreateMap<Clinic, ClinicDto>();
        CreateMap<TimeSlot, TimeSlotDto>();
        CreateMap<Appointment, AppointmentDto>();
    }
}

