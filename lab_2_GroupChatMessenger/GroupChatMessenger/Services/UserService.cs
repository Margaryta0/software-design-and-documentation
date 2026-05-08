using GroupChatMessenger.Models;
using GroupChatMessenger.Storage;

namespace GroupChatMessenger.Services;

public class UserService
{
    private readonly Database _db;

    public UserService(Database db)
    {
        _db = db;
    }

    public User CreateUser(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("User name cannot be empty.");

        var user = new User { Name = name.Trim() };
        _db.SaveUser(user);
        return user;
    }

    public User GetUser(string id)
    {
        return _db.GetUser(id)
            ?? throw new KeyNotFoundException($"User '{id}' not found.");
    }

    public List<User> GetAllUsers() => _db.GetAllUsers();

    public bool UserExists(string id) => _db.GetUser(id) != null;
}
