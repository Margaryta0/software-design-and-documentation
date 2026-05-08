using GroupChatMessenger.Models;
using GroupChatMessenger.Storage;

namespace GroupChatMessenger.Services;

// Delivery Service: processes tasks from the queue and updates
// per-recipient delivery statuses on messages.
// In production this would run as a background worker.
public class DeliveryService
{
    private readonly Database _db;

    public DeliveryService(Database db)
    {
        _db = db;
    }

    //simulates async worker
    public List<string> ProcessPendingDeliveries()
    {
        var tasks = _db.DequeuePendingTasks(limit: 50);
        var processedMessageIds = new List<string>();

        foreach (var task in tasks)
        {
            var message = _db.GetMessage(task.MessageId);
            if (message == null)
            {
                _db.DeleteDeliveryTask(task.Id);
                continue;
            }

            var status = message.DeliveryStatuses
                .FirstOrDefault(s => s.RecipientId == task.RecipientId);

            if (status != null)
            {
                status.State = DeliveryState.Delivered;
                status.DeliveredAt = DateTime.UtcNow;
            }

            UpdateMessageStatus(message);
            _db.SaveMessage(message);
            _db.DeleteDeliveryTask(task.Id);

            processedMessageIds.Add(task.MessageId);
        }

        return processedMessageIds.Distinct().ToList();
    }

    public void MarkAsRead(string messageId, string recipientId)
    {
        var message = _db.GetMessage(messageId)
            ?? throw new KeyNotFoundException($"Message '{messageId}' not found.");

        var status = message.DeliveryStatuses
            .FirstOrDefault(s => s.RecipientId == recipientId)
            ?? throw new KeyNotFoundException($"Recipient '{recipientId}' has no delivery record for this message.");

        if (status.State == DeliveryState.Delivered || status.State == DeliveryState.Read)
        {
            status.State = DeliveryState.Read;
            status.ReadAt = DateTime.UtcNow;
        }

        UpdateMessageStatus(message);
        _db.SaveMessage(message);
    }

    private static void UpdateMessageStatus(Message message)
    {
        var statuses = message.DeliveryStatuses;
        if (!statuses.Any()) return;

        var allRead = statuses.All(s => s.State == DeliveryState.Read);
        var anyRead = statuses.Any(s => s.State == DeliveryState.Read);
        var allDelivered = statuses.All(s => s.State is DeliveryState.Delivered or DeliveryState.Read);
        var anyDelivered = statuses.Any(s => s.State is DeliveryState.Delivered or DeliveryState.Read);

        message.Status = (allRead, anyRead, allDelivered, anyDelivered) switch
        {
            (true, _, _, _) => MessageStatus.FullyRead,
            (_, true, _, _) => MessageStatus.PartiallyRead,
            (_, _, true, _) => MessageStatus.FullyDelivered,
            (_, _, _, true) => MessageStatus.PartiallyDelivered,
            _ => message.Status // no change
        };
    }
}
