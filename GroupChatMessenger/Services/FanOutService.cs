using GroupChatMessenger.Models;
using GroupChatMessenger.Storage;

namespace GroupChatMessenger.Services;

public class FanOutService
{
    private readonly Database _db;

    public FanOutService(Database db)
    {
        _db = db;
    }

    public List<DeliveryTask> FanOut(Message message, List<string> recipientIds)
    {
        var tasks = new List<DeliveryTask>();

        foreach (var recipientId in recipientIds)
        {
            if (recipientId == message.SenderId) continue;

            var task = new DeliveryTask
            {
                MessageId = message.Id,
                RecipientId = recipientId
            };

            _db.EnqueueDeliveryTask(task);
            tasks.Add(task);
        }

        return tasks;
    }
}
