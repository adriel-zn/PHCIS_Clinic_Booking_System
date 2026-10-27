namespace PHCIS.Web.Models;

public sealed record AdministratorDetails(string Id, string Email, bool IsCurrentUser, bool CanRemove);
