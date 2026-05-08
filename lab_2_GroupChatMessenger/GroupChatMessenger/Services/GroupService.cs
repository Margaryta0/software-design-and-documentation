using GroupChatMessenger.Models;
using GroupChatMessenger.Storage;

namespace GroupChatMessenger.Services;

public class GroupService
{
    private readonly Database _db;
    private readonly UserService _userService;

    public GroupService(Database db, UserService userService)
    {
        _db = db;
        _userService = userService;
    }

    public Group CreateGroup(string name, List<string> memberIds)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name cannot be empty.");

        if (memberIds == null || memberIds.Count < 2)
            throw new ArgumentException("A group must have at least 2 members.");

        foreach (var memberId in memberIds)
        {
            if (!_userService.UserExists(memberId))
                throw new KeyNotFoundException($"User '{memberId}' not found.");
        }

        var group = new Group
        {
            Name = name.Trim(),
            MemberIds = memberIds.Distinct().ToList()
        };

        _db.SaveGroup(group);
        return group;
    }

    public Group GetGroup(string id)
    {
        return _db.GetGroup(id)
            ?? throw new KeyNotFoundException($"Group '{id}' not found.");
    }

    public List<Group> GetAllGroups() => _db.GetAllGroups();

    public void AddMember(string groupId, string userId)
    {
        var group = GetGroup(groupId);
        if (!_userService.UserExists(userId))
            throw new KeyNotFoundException($"User '{userId}' not found.");

        if (!group.MemberIds.Contains(userId))
        {
            group.MemberIds.Add(userId);
            _db.SaveGroup(group);
        }
    }
}
