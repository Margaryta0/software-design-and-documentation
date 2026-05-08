namespace GroupChatMessenger.Models;

public class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string GroupId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public MessageStatus Status { get; set; } = MessageStatus.Created;

    public List<DeliveryStatus> DeliveryStatuses { get; set; } = new(); 
}

public class DeliveryStatus
{
    public string RecipientId { get; set; } = string.Empty;
    public DeliveryState State { get; set; } = DeliveryState.Pending;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public enum MessageStatus
{
    Created,
    Stored,
    FanOutTriggered,
    DeliveryInProgress,
    PartiallyDelivered,
    FullyDelivered,
    PartiallyRead,
    FullyRead
}

public enum DeliveryState
{
    Pending,
    Enqueued,
    Delivered,
    Read
}
