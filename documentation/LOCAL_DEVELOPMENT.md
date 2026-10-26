# PHCIS Local Development

## Local Development: SQL Server and EF Identity

The portal now uses simple email/password login with ASP.NET Core Identity and EF Core. External OIDC/JWT configuration has been removed. Passwords are hashed by Identity, and sessions use secure HttpOnly cookies.

### Use Your Own SQL Server

Configure your SQL Server connection below, then follow **Run from Visual Studio**. No PowerShell scripts are required.

Registration and login use ASP.NET Core Identity through EF Core in the same SQL Server database as patients and appointments. Set the connection string and start the API: it applies pending migrations before accepting requests. You do not need LocalDB for this workflow.

In `backend/PHCIS.Api/appsettings.Development.json`, replace the existing `ConnectionStrings:Phcis` value with your SQL Server connection. For Windows authentication:

~~~json
"ConnectionStrings": {
  "Phcis": "Server=YOUR_SERVER\\YOUR_INSTANCE;Database=PHCIS;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
}
~~~

Keep the other JSON sections. The base `backend/PHCIS.Api/appsettings.json` also exposes `ConnectionStrings:Phcis` and enables startup migrations:

~~~json
"Database": {
  "ApplyMigrationsOnStartup": true
}
~~~

Development settings override the base file, so edit the Development connection when using the https launch profile. Start both projects from Visual Studio as described below.

Alternatively, set `ConnectionStrings:Phcis` in API user secrets or the `ConnectionStrings__Phcis` environment variable. These override the JSON setting; remove an old override if edits to JSON appear ineffective. For SQL authentication, use `User ID=YOUR_LOGIN;Password=YOUR_PASSWORD` instead of `Integrated Security=True`, and keep credentials in local user secrets rather than committing them. `TrustServerCertificate=True` is for a local development server with an untrusted certificate; use a trusted SQL Server certificate and `TrustServerCertificate=False` elsewhere.

The SQL Server instance must already be installed and reachable. The connection identity needs permission to create the database (if missing) and apply schema changes. Startup applies only migrations missing from EF's migration history and stops if migration fails, rather than serving requests against an outdated schema. Demo account seeding remains separate and disabled by default (`Development:InitializeDatabase=false`); clinic/slot seeding is enabled in Development through `Development:SeedClinicsAndSlots`. To manage migrations manually, set `Database:ApplyMigrationsOnStartup=false`, then run:

~~~powershell
dotnet tool restore
dotnet ef database update --project backend/PHCIS.Infrastructure --startup-project backend/PHCIS.Api -- --environment Development
~~~

Open [Register](https://localhost:7100/register) to create an account, then sign in. Registration uses the same background and card design as login. Identity stores password hashes in `AspNetUsers` and links each account to its `Patients` record. Migrations create the schema; the enabled Development clinic/slot seed then supplies sample availability. It creates no demo account. The optional demo account initializer below remains restricted to the dedicated LocalDB demo database.

### Run from Visual Studio

Development startup also seeds four fictional clinics (Central Community Clinic, Westville Clinic, Phoenix Clinic and Umlazi Clinic) and future 30-minute slots, Monday–Friday, 08:00–17:00 South African time, across a rolling 15-calendar-day window. This uses your configured SQL Server, including SQL Express, and runs after migrations. Existing clinics, slots and appointments are preserved; repeated startups add missing records only. No patient account is created by this seed option; use Register.

The option in `appsettings.Development.json` is `Development:SeedClinicsAndSlots=true`. Set it to false to stop refreshing sample availability. It never runs outside Development. The separate `Development:InitializeDatabase` option remains false and is only for the optional LocalDB demo account below.

1. Open `PHCIS_Clinic_Booking_System.slnx`.
2. Select **API and Blazor** in the startup toolbar, then press **F5**. The shared `.slnLaunch` profile starts both projects using HTTPS.
3. If the profile is not visible, right-click the solution, choose **Configure Startup Projects**, select **Multiple startup projects**, and set **PHCIS.Api** and **PHCIS.Web** to **Start**, with the API first. Set every other project to **None** and choose **https** for both debug targets.
4. The frontend opens https://localhost:7100/login. The API runs at https://localhost:7014 and applies pending migrations before accepting requests.
5. Use **Shift+F5** (Stop Debugging) before rebuilding or starting again. Do not launch a second copy from another Visual Studio instance or a terminal.

Multi-project launch profiles require Visual Studio 2022 17.11 or later; if needed, enable **Multi-Project Launch Profiles** under **Tools > Options > Preview Features**. See [Microsoft's setup guide](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-set-multiple-startup-projects?view=visualstudio).

If an old background copy still holds an executable or port, stop that PHCIS process in Task Manager before pressing F5. No launcher scripts are included.

### Add Clinics Through the App

Clinic management is available at `/manage-clinics`. Only explicitly authorized accounts can create clinics; normal registered patients cannot grant themselves access.

1. Register your own account and sign in.
2. In Visual Studio **SQL Server Object Explorer** (or SSMS), connect to your configured database. Find your account in `dbo.AspNetUsers` by `Email` and copy its `Id` (the Identity account ID, not `PatientId`).
3. In **PHCIS.Api > Manage User Secrets**, add the following section alongside your existing settings, replacing the example with that ID:

~~~json
"ClinicManagement": {
  "AdministratorAccountIds": ["YOUR-ASPNETUSERS-ID"]
}
~~~

The same section is available in API `appsettings.json`; use Development settings or user secrets for your local account. The allowlist is empty by default. Account IDs are server assigned and cannot be chosen during registration. Add more IDs to authorize additional administrators.

4. Stop debugging, restart **API and Blazor** with F5, and open **Clinics > Manage clinics**.
5. Enter a unique clinic name, address and optional phone number, then select **Add clinic**. The API saves the clinic and its initial future slots in one transaction. Slots cover the next 15 calendar days, Monday–Friday, 08:00–17:00 South African time, in 30-minute intervals.
6. Select **View clinics** to book against the new availability.

Clinic details are validated in the browser and API. Duplicate clinic names are rejected. The API checks administrator access and CSRF protection for every creation request. This page adds clinics; editing/deleting clinics or managing custom opening schedules is not included. Initial availability for manually added clinics is created once at creation.

Endpoints: `GET /api/clinic-management/access` reports current-account access; `POST /api/clinic-management` creates a clinic and initial slots and returns its ID. Anonymous requests are rejected and non-administrator writes return 403.

### Optional LocalDB Demo

The demo initializer remains available only for SQL Server LocalDB / PHCIS.Development. The demo account below exists only after initialization; a new database otherwise requires registration.

Open [the patient portal](https://localhost:7100). The API runs at https://localhost:7014 and Development Swagger is at https://localhost:7014/swagger/index.html.

Demo account:

| Field | Value |
| --- | --- |
| Email | patient@phcis.local |
| Password | ClinicDemo!2026 |
| Name | Thando Mokoena |

These credentials are for the fictional local demonstration only. You can also choose **Register** to create a new patient account. Registration requires names, email, phone number, date of birth, and a password of at least 10 characters containing uppercase, lowercase, a digit, and a symbol. ID number is optional.

If the HTTPS certificate is not trusted on another development machine, run:

~~~powershell
dotnet dev-certs https --trust
~~~

### Database and Seed Behavior

The Development connection string is in backend/PHCIS.Api/appsettings.Development.json:

~~~text
Server=(localdb)\MSSQLLocalDB;Database=PHCIS.Development;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True
~~~

TrustServerCertificate is confined to this local Development connection. Production must supply its own connection and certificate configuration.

To inspect the database in SQL Server Management Studio, connect to (localdb)\MSSQLLocalDB with Windows Authentication and open PHCIS.Development. Identity credentials are stored in AspNetUsers as password hashes; patient and booking records remain in their own tables.

To enable optional demo seeding from Visual Studio, set Development:InitializeDatabase to true in appsettings.Development.json and add Development:SeedPassword through **PHCIS.Api > Manage User Secrets**. Start with F5, then restore InitializeDatabase to false after initialization. The initializer refuses any target except PHCIS.Development on (localdb)\MSSQLLocalDB and never runs outside Development.

Seed data contains one fictional patient account, four clinics matching the reference design's examples, and 30-minute weekday slots from 08:00 to 17:00, covering a rolling 15-day window. Past slots are not newly seeded. If today's opening hours have ended, choose the next weekday to test availability. Existing accounts, appointments, passwords, and clinic records are not reset or deleted.

The seed password comes from Development:SeedPassword in API user secrets and is used only when the demo account is first created.

Changing that setting does not reset an existing account's password. Forgotten-password email delivery is not implemented; the UI directs users to contact their clinic. No email or SMS confirmation is claimed or sent.

### End-to-End Walkthrough

1. Sign in with the demo account, or register a new account.
2. Open **Clinics** or **Book Appointment**, and select **View Available Slots**.
3. Choose a future weekday, then **Find available slots**.
4. Select a time and continue to patient details.
5. Choose the reason for the visit, select **Next**, review, and **Confirm Booking**.
6. Open **My Appointments**. The saved visit appears in **Upcoming**.
7. **Reschedule** selects another future slot at the same clinic, preserving the appointment reference.
8. **Cancel** opens a summary. Enter a reason, check the confirmation box, and submit.
9. The visit remains under **Cancelled** and its slot becomes available again.
10. Open **Profile** to edit demographics, phone number, or optional ID number. Email is read-only in this flow.

The dashboard counts actual persisted appointments. There is no fabricated Pending state. Upcoming means a Scheduled appointment whose slot has not started.

Use the walkthrough above to check booking and cancellation against your configured SQL Server from the Visual Studio session.

## Folder Structure

~~~text
PHCIS/
├── PHCIS_Clinic_Booking_System.slnx
├── backend/
│   ├── PHCIS.Api/
│   │   ├── Authentication/
│   │   ├── Contracts/
│   │   ├── Controllers/
│   │   ├── Development/
│   │   └── Errors/
│   ├── PHCIS.Application/
│   │   ├── Abstractions/
│   │   ├── Models/
│   │   ├── Services/
│   │   ├── Mapping/
│   │   └── Validation/
│   ├── PHCIS.Domain/
│   │   ├── Common/
│   │   ├── Entities/
│   │   └── Exceptions/
│   ├── PHCIS.Infrastructure/
│   │   ├── Identity/
│   │   └── Persistence/
│   │       ├── Configurations/
│   │       ├── Migrations/
│   │       └── Repositories/
│   └── PHCIS.UnitTests/
├── frontend/
│   ├── PHCIS.Web/
│   │   ├── Components/
│   │   ├── Configuration/
│   │   ├── Layout/
│   │   ├── Models/
│   │   ├── Pages/
│   │   ├── Services/
│   │   └── wwwroot/
│   └── PHCIS.Web.Tests/
└── documentation/
~~~

Generated bin and obj folders are omitted. All seven .NET 10 projects are in the existing solution. Frontend remains separate and uses HTTP contracts, with no backend project references.

## Layer References and Responsibilities

| Project | Project references | Responsibility |
| --- | --- | --- |
| Domain | None | Encapsulated entities and lifecycle rules |
| Application | Domain | Use-case services, DTOs, validation, and persistence abstractions |
| Infrastructure | Application, Domain | SQL Server/EF mappings, Identity stores, repositories, migrations |
| Api | Application, Infrastructure | Controllers, local account/session integration, authorization, CSRF, errors, Swagger |
| UnitTests | Api, Application, Domain, Infrastructure | Domain, service, persistence-model, and HTTP tests |
| Web | No backend references | Standalone Blazor WebAssembly patient UI and HTTP clients |
| Web.Tests | Web | Component, form, HTTP-client, and cookie/CSRF pipeline tests |

Application services coordinate booking/cancellation/rescheduling and commit through a scoped unit of work. Domain remains independent of EF and HTTP. The API's account service coordinates Identity and linked patient creation in one SQL transaction. Read models provide clinic listings, patient-owned appointment history, and profile data.

The backend uses EF Core, AutoMapper, FluentValidation, and Serilog. Dapper and PostgreSQL are not configured. AutoMapper's license can be supplied through AutoMapper:LicenseKey when required; consult its [licensing documentation](https://docs.automapper.io/en/latest/License-configuration.html). Its development license warning is expected without a key.

## Domain Rules and SQL Constraints

- Entities have private setters, validated operations, read-only navigation collections, and defensive concurrency-token getters.
- Slot times and clinic linkage are immutable. Times are stored as datetimeoffset and displayed in clinic time, UTC+02:00 by default.
- Booking requires an existing patient and a future slot. Each slot permits one non-cancelled appointment.
- Cancellation requires Scheduled status, a nonblank reason of at most 500 trimmed characters, and a time strictly before slot start.
- Completion and no-show require Scheduled status and a time at or after slot end.
- Cancelled, Completed, and NoShow are terminal.
- Rescheduling requires a Scheduled appointment that has not started, a future replacement slot, and the same clinic. The appointment ID is preserved.
- Referenced records cannot be cascade-deleted. SQL rowversion detects conflicting updates.
- A filtered unique index resolves competing bookings; check constraints protect slot duration, valid statuses, and cancellation metadata.

Current persisted status values are Cancelled=0, Scheduled=1, Completed=2, and NoShow=3. AddLocalIdentityAndPortal converts rows written using the earlier Scheduled=0 / Cancelled=1 mapping and updates the cancellation constraint and filtered index. Historical migrations remain intact.

Patient overlap across distinct slots and clinic-capacity policies beyond one appointment per slot are not implemented.

### Entity Relationship Diagram

~~~mermaid
erDiagram
    IDENTITY_USER ||--|| PATIENT : links_to
    PATIENT ||--o{ APPOINTMENT : has
    CLINIC ||--o{ TIME_SLOT : offers
    TIME_SLOT ||--o{ APPOINTMENT : retains_history
    IDENTITY_USER {
        string Id PK
        guid PatientId FK
        string Email
        string PasswordHash
    }
    PATIENT {
        guid Id PK
        string FirstName
        string LastName
        date DateOfBirth
        string PhoneNumber
        string Email
        string NationalId
        rowversion RowVersion
    }
    CLINIC {
        guid Id PK
        string Name
        string Address
        string PhoneNumber
        rowversion RowVersion
    }
    TIME_SLOT {
        guid Id PK
        guid ClinicId FK
        datetimeoffset StartsAt
        datetimeoffset EndsAt
        rowversion RowVersion
    }
    APPOINTMENT {
        guid Id PK
        guid PatientId FK
        guid TimeSlotId FK
        int Status
        string ReasonForVisit
        datetimeoffset CreatedAt
        datetimeoffset CancelledAt
        string CancellationReason
        rowversion RowVersion
    }
~~~

### Appointment Lifecycle

~~~mermaid
stateDiagram-v2
    [*] --> Scheduled: Book before slot start
    Scheduled --> Scheduled: Reschedule to a future slot at the same clinic
    Scheduled --> Cancelled: Cancel before start with reason
    Scheduled --> Completed: Complete at or after end
    Scheduled --> NoShow: Mark missed at or after end
    Cancelled --> [*]
    Completed --> [*]
    NoShow --> [*]
~~~

## API and Authentication

| Method/path | Purpose |
| --- | --- |
| GET /api/account/csrf | Get a request token and antiforgery cookie |
| POST /api/account/register | Create an EF Identity account and linked patient |
| POST /api/account/login | Verify password and establish an HttpOnly cookie |
| POST /api/account/logout | End the current session |
| GET /api/account/me | Read the authenticated patient's profile |
| POST /api/account/profile | Update that patient's demographics/contact details |
| GET /api/clinics | List clinics |
| GET /api/clinics/{clinicId}/available-slots | Search available future slots |
| GET /api/clinics/{clinicId}/slots/{slotId} | Read a slot for booking review |
| GET /api/appointments | Read only the patient's own appointment history |
| POST /api/appointments | Book a slot; body has timeSlotId and optional reasonForVisit |
| POST /api/appointments/{id}/cancellation | Cancel; body has reason |
| POST /api/appointments/{id}/reschedule | Reschedule; body has timeSlotId |

Account login/registration and the CSRF token endpoint allow anonymous access; all patient data and scheduling endpoints require an authenticated linked account. The server adds patient_id from the persisted Identity-to-Patient relationship. Client bodies cannot choose another patient.

Every unsafe request, including registration, login, and logout, must first obtain a fresh requestToken from GET /api/account/csrf and send it in X-CSRF-TOKEN. The frontend's CookieSessionHandler does this automatically and includes browser cookie credentials only for the configured API.

The session cookie is __Host-PHCIS: Secure, HttpOnly, SameSite=Strict. Remember me makes it persistent; the configured lifetime is eight hours with sliding renewal. Identity locks an account for 15 minutes after five failed password attempts. Authentication failures use a generic incorrect-email/password message.

The Development API allows credentialed CORS only from https://localhost:7100. Additional origins require explicit Cors:AllowedOrigins configuration. CORS does not bypass authentication or CSRF. Production hosting must retain HTTPS and same-site cookie semantics, or deliberately configure a reviewed cross-site deployment.

### Login Sequence

~~~mermaid
sequenceDiagram
    participant Browser as Blazor
    participant API
    participant Identity
    participant SQL as SQL Server
    Browser->>API: GET account/csrf with credentials
    API-->>Browser: Antiforgery cookie and request token
    Browser->>API: POST account/login with CSRF header
    API->>Identity: Check password and lockout
    Identity->>SQL: Read EF Identity account
    SQL-->>Identity: Password hash and patient linkage
    Identity-->>API: Authenticated claims
    API-->>Browser: Secure HttpOnly session cookie
    Browser->>API: GET account/me with cookie
    API-->>Browser: Patient profile
~~~

Availability filters slot start instants using From inclusive and To exclusive, strictly after now. Offset must be nonnegative and Limit must be 1–100. Results are ordered by start time then ID and are snapshots, not reservations. The frontend converts the selected clinic-local calendar day into an explicitly offset interval.

### Errors and Validation

| Status | Meaning |
| --- | --- |
| 400 | Invalid body/query, field validation, or failed CSRF verification |
| 401 | Not signed in, invalid/expired session, or incorrect login |
| 403 | Authenticated session lacks a valid patient identity |
| 404 | Resource missing or outside the patient's scope |
| 409 | Occupied slot, invalid lifecycle timing/state, or concurrent write |
| 415 | Unsupported request content type |
| 500 | Unexpected failure with safe generic details |

Errors use application/problem+json with a traceId; validation errors include a field-keyed errors object. Internal exception messages, passwords, and raw database details are not returned. The UI displays safe errors and support references. Writes are never automatically retried by the frontend; network/time-out failures warn when the result might be uncertain.

## Frontend Design and Configuration

The UI follows the supplied reference: navy sidebar, blue actions, compact patient dashboard, clinic cards, date/time selection, patient review and confirmation steps, appointment tabs/actions, cancellation confirmation, and profile editing. Layouts adapt to narrow screens, controls are labeled, and focus/error/loading states are provided.

Verified UI previews: [login](images/portal-login.png), [dashboard](images/portal-dashboard.png), and [clinic directory](images/portal-clinics.png).

The clinic image is a generated fictional exterior, reused illustratively rather than claiming to depict the named demo clinics:
- Asset: frontend/PHCIS.Web/wwwroot/images/clinic-exterior.png
- Built-in image-generation mode; no external stock-photo service.
- Exact generation prompt is saved beside the asset as clinic-exterior.prompt.txt.

Public browser settings are in frontend/PHCIS.Web/wwwroot/appsettings.json:
- Api:BaseUrl defaults to https://localhost:7014/.
- Api:ClinicUtcOffsetMinutes defaults to 120.
- Api:DefaultClinicId is optional for the reusable search component.
- No OIDC authority, client ID, client secret, pasted token, or API scope is needed.

Do not put secrets in wwwroot. Credentials are submitted only to the local Identity login endpoint; the frontend stores no passwords or bearer tokens in browser storage. Authentication state is rehydrated from account/me after refresh.

## Migrations, Tests, and Publishing

~~~powershell
dotnet tool restore
dotnet restore PHCIS_Clinic_Booking_System.slnx
dotnet build PHCIS_Clinic_Booking_System.slnx
dotnet test PHCIS_Clinic_Booking_System.slnx --no-restore

# Review a future schema change:
dotnet ef migrations add YourMigrationName --project backend/PHCIS.Infrastructure --startup-project backend/PHCIS.Api --output-dir Persistence/Migrations
dotnet ef migrations script --idempotent --project backend/PHCIS.Infrastructure --startup-project backend/PHCIS.Api

# Standalone static frontend output:
dotnet publish frontend/PHCIS.Web -c Release
~~~

Tests cover Domain invariants, appointment services, EF mappings/migration SQL, real Identity registration/password hashing/login/logout, lockout, CSRF, authorization and patient isolation, profile updates, rescheduling, HTTP errors, CORS, Swagger, Blazor components/forms, and HTTP-cookie behavior. In-memory EF tests do not prove SQL Server constraints; the browser walkthrough additionally exercises the actual SQL Server database.

The root .editorconfig defines four-space C# indentation and separate-line braces. Apply the formatter with:

~~~powershell
dotnet format PHCIS_Clinic_Booking_System.slnx whitespace --no-restore --exclude backend/PHCIS.Infrastructure/Persistence/Migrations
~~~

The WebAssembly publish folder must be hosted with correct framework MIME/content-encoding support and an index.html fallback for client routes. No public deployment has been made. Production still needs explicit configuration, recovery/verification email, operational monitoring, and its own secret/key management.

Implementation references: [ASP.NET Core Identity with standalone Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/standalone-with-identity/?view=aspnetcore-10.0), [Identity EF model customization](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-primary-key-configuration), [EF concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), and [bUnit](https://bunit.dev/docs/test-doubles/auth.html).

