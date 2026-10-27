using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PHCIS.Api.Authentication;

namespace PHCIS.Api.Realtime;

/// <summary>Server-pushed refresh signals only: no client-selected groups and no patient payloads.</summary>
[Authorize(Policy = PatientIdentity.Policy)]
public sealed class AppointmentHub : Hub { }
