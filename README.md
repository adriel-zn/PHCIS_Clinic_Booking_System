# PHCIS Clinic Booking System

## Scenario Overview

You are tasked with designing and implementing a simplified Clinic Booking System for PHCIS (Primary Health Care Information System). The system should allow patients to book appointments at clinics, view available time slots, and receive confirmation. 

## Software Requirements Specification (SRS)

**Project:** Primary Health Care Information System (PHCIS)  
**Module:** Clinic Appointment Booking System  
**Document Type:** Requirements Analysis  
**Version:** 1.0  
**Status:** Initial Requirements Specification

### 1.1 Purpose

The purpose of this document is to define the functional and non-functional requirements for the PHCIS Clinic Booking System.

The system is designed to simplify the process of booking, managing, and tracking clinic appointments within primary healthcare facilities.

This document serves as a reference for system design, development, testing, and future maintenance.


### 1.2 Technologies

The system must use the technologies and development tools permitted or required by the assessment instructions.

## Folder Structure

```text
PHCIS/
├── PHCIS_Clinic_Booking_System.slnx
├── backend/
│   ├── PHCIS.Api/
│   │   ├── PHCIS.Api.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── Properties/
│   │       └── launchSettings.json
│   ├── PHCIS.Application/
│   │   └── PHCIS.Application.csproj
│   ├── PHCIS.Domain/
│   │   └── PHCIS.Domain.csproj
│   ├── PHCIS.Infrastructure/
│   │   └── PHCIS.Infrastructure.csproj
│   └── PHCIS.UnitTests/
│       └── PHCIS.UnitTests.csproj
├── documentation/
├── README.md
└── LICENSE
```

Generated `bin` and `obj` folders are omitted. All five backend projects target .NET 10 and are included in the existing solution. No frontend folder has been created; frontend work should remain separate from `backend`, at the repository root.

## Layer References

Each arrow means "has a project reference to":

```text
PHCIS.Api            -> PHCIS.Application, PHCIS.Infrastructure
PHCIS.Application    -> PHCIS.Domain
PHCIS.Infrastructure -> PHCIS.Application, PHCIS.Domain
PHCIS.UnitTests      -> PHCIS.Application, PHCIS.Domain
PHCIS.Domain         -> (no project references)
```

- **PHCIS.Domain** is intended to hold core entities and business rules. It has no dependencies on the other projects.
- **PHCIS.Application** is intended to hold use cases and interfaces, using Domain types.
- **PHCIS.Infrastructure** is intended to implement Application interfaces for persistence and external services, using Domain types where needed.
- **PHCIS.Api** hosts HTTP endpoints and wires Application services to Infrastructure implementations.
- **PHCIS.UnitTests** is an xUnit project that can test Application and Domain directly.

Dependencies point toward the core: Domain remains independent, and Application does not reference Infrastructure or Api. Api references Infrastructure to compose the application's implementations at startup.

The projects currently contain scaffolding only; no booking behavior has been implemented.

