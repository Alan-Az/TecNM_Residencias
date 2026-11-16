namespace TecNM.Residency.Auth;

public interface IAuthRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByExternalUserIdAsync(Guid externalUserId);
    Task<User> AddUserAsync(User user);
    Task UpdateUserAsync(User user);
    Task UpdatePasswordHashAsync(long userId, string newHash);
    Task<Dictionary<long, string>> GetUserDisplayNamesByIdsAsync(List<long> userIds);
}
