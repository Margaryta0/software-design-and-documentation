using GroupChatMessenger.Models;
using GroupChatMessenger.Storage;

namespace GroupChatMessenger.Services;

/// <summary>
/// Core orchestrator for sending messages.
/// Flow: validate → save → fan-out → return 202 Accepted
/// </summary>
public class MessageService
{
    private readonly Database _db;
    private readonly GroupService _groupService;
    private readonly UserService _userService;
    private readonly FanOutService _fanOutService;

    public MessageService(
        Database db,
        GroupService groupService,
        UserService userService,
        FanOutService fanOutService)
    {
        _db = db;
        _groupService = groupService;
        _userService = userService;
        _fanOutService = fanOutService;
    }

    public Message SendMessage(string groupId, string senderId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Message text cannot be empty.");

        var group = _groupService.GetGroup(groupId); 
        var sender = _userService.GetUser(senderId); 

        if (!group.MemberIds.Contains(senderId))
            throw new InvalidOperationException($"User '{sender.Name}' is not a member of group '{group.Name}'.");

        // Build initial delivery statuses for all recipients (except sender)
        var recipients = group.MemberIds.Where(id => id != senderId).ToList();
        var deliveryStatuses = recipients.Select(recipientId => new DeliveryStatus
        {
            RecipientId = recipientId,
            State = DeliveryState.Pending
        }).ToList();

        var message = new Message
        {
            GroupId = groupId,
            SenderId = senderId,
            Text = text.Trim(),
            Status = MessageStatus.Stored,
            DeliveryStatuses = deliveryStatuses
        };

        _db.SaveMessage(message);

        message.Status = MessageStatus.FanOutTriggered;
        _fanOutService.FanOut(message, group.MemberIds);

        foreach (var ds in message.DeliveryStatuses)
            ds.State = DeliveryState.Enqueued;

        message.Status = MessageStatus.DeliveryInProgress;
        _db.SaveMessage(message);

        return message;
    }

    public List<Message> GetGroupMessages(string groupId)
    {
        _groupService.GetGroup(groupId); // validates group exists
        return _db.GetGroupMessages(groupId);
    }

    public Message GetMessage(string messageId)
    {
        return _db.GetMessage(messageId)
            ?? throw new KeyNotFoundException($"Message '{messageId}' not found.");
    }
}
