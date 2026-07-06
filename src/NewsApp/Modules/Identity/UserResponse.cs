namespace NewsApp.Modules.Identity;

public record UserResponse(string Id, string Email, IList<string> Roles);