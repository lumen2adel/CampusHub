namespace CampusHub.Classes.UserAccount
{
    /// <summary>
    /// Public view of a user in search results. An explicit allow-list: fields added to
    /// AppUser later (or credentials already on it) are never exposed by accident.
    /// </summary>
    public record UserSearchResult(Guid Id, string? FirstName, string? LastName);
}
