namespace GroupChatMessenger.Api;


public record CreateUserRequest(string Name);

public record CreateGroupRequest(string Name, List<string> MemberIds);

public record AddMemberRequest(string UserId);

public record SendMessageRequest(string SenderId, string Text);

public record MarkReadRequest(string RecipientId);



public record UserResponse(string Id, string Name, DateTime CreatedAt);

public record GroupResponse(string Id, string Name, List<string> MemberIds, DateTime CreatedAt);

public record DeliveryStatusResponse(string RecipientId, string State, DateTime? DeliveredAt, DateTime? ReadAt);

public record MessageResponse(
    string Id,
    string GroupId,
    string SenderId,
    string Text,
    DateTime CreatedAt,
    string Status,
    List<DeliveryStatusResponse> DeliveryStatuses);

public record ErrorResponse(string Error);

public record ProcessDeliveriesResponse(int ProcessedCount, List<string> MessageIds);
