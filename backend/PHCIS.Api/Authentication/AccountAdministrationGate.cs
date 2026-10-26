namespace PHCIS.Api.Authentication;
/// <summary>Serializes account state and Admin membership changes within one host.</summary>
public static class AccountAdministrationGate
{
    /// <summary>Defines the new contract for the platform integration.</summary>
    public static readonly SemaphoreSlim Changes = new(1, 1);
}
