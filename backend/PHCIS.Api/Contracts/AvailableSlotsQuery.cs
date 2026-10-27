using System.ComponentModel.DataAnnotations;

namespace PHCIS.Api.Contracts;

/// <summary>UTC or explicitly offset slot-start search interval and bounded pagination.</summary>
public sealed class AvailableSlotsQuery
{
    /// <summary>Inclusive lower bound for slot start instants.</summary>
    [Required]
    public DateTimeOffset? From { get; init; }

    /// <summary>Exclusive upper bound for slot start instants.</summary>
    [Required]
    public DateTimeOffset? To { get; init; }

    /// <summary>Number of matching slots to skip; defaults to zero.</summary>
    public int Offset { get; init; }
    /// <summary>Page size from 1 to 100; defaults to 50.</summary>
    public int Limit { get; init; } = 50;
}

