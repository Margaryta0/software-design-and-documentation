using GroupChatMessenger.Models;
using GroupChatMessenger.Services;

namespace GroupChatMessenger.Api;

public static class Routes
{
    public static void MapRoutes(WebApplication app)
    {
        MapUserRoutes(app);
        MapGroupRoutes(app);
        MapMessageRoutes(app);
        MapDeliveryRoutes(app);
    }


    private static void MapUserRoutes(WebApplication app)
    {
        // POST /users — Create a new user
        app.MapPost("/users", (CreateUserRequest req, UserService svc) =>
        {
            try
            {
                var user = svc.CreateUser(req.Name);
                return Results.Created($"/users/{user.Id}", MapUser(user));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse(ex.Message));
            }
        });

        // GET /users — List all users
        app.MapGet("/users", (UserService svc) =>
            Results.Ok(svc.GetAllUsers().Select(MapUser)));

        // GET /users/{id} — Get user by ID
        app.MapGet("/users/{id}", (string id, UserService svc) =>
        {
            try { return Results.Ok(MapUser(svc.GetUser(id))); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new ErrorResponse(ex.Message)); }
        });
    }


    private static void MapGroupRoutes(WebApplication app)
    {
        // POST /groups — Create a group
        app.MapPost("/groups", (CreateGroupRequest req, GroupService svc) =>
        {
            try
            {
                var group = svc.CreateGroup(req.Name, req.MemberIds);
                return Results.Created($"/groups/{group.Id}", MapGroup(group));
            }
            catch (ArgumentException ex) { return Results.BadRequest(new ErrorResponse(ex.Message)); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new ErrorResponse(ex.Message)); }
        });

        // GET /groups — List all groups
        app.MapGet("/groups", (GroupService svc) =>
            Results.Ok(svc.GetAllGroups().Select(MapGroup)));

        // GET /groups/{id} — Get group by ID
        app.MapGet("/groups/{id}", (string id, GroupService svc) =>
        {
            try { return Results.Ok(MapGroup(svc.GetGroup(id))); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new ErrorResponse(ex.Message)); }
        });

        // POST /groups/{id}/members — Add member to group
        app.MapPost("/groups/{id}/members", (string id, AddMemberRequest req, GroupService svc) =>
        {
            try
            {
                svc.AddMember(id, req.UserId);
                return Results.Ok(MapGroup(svc.GetGroup(id)));
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new ErrorResponse(ex.Message)); }
        });
    }

    private static void MapMessageRoutes(WebApplication app)
    {
        // POST /groups/{id}/messages — Send message to group
        app.MapPost("/groups/{id}/messages", (string id, SendMessageRequest req, MessageService svc) =>
        {
            try
            {
                var message = svc.SendMessage(id, req.SenderId, req.Text);
                // 202 Accepted — fan-out happens async
                return Results.Accepted($"/groups/{id}/messages", MapMessage(message));
            }
            catch (ArgumentException ex) { return Results.BadRequest(new ErrorResponse(ex.Message)); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new ErrorResponse(ex.Message)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new ErrorResponse(ex.Message)); }
        });

        // GET /groups/{id}/messages — Get message history for group
        app.MapGet("/groups/{id}/messages", (string id, MessageService svc) =>
        {
            try { return Results.Ok(svc.GetGroupMessages(id).Select(MapMessage)); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new ErrorResponse(ex.Message)); }
        });

        // GET /messages/{id} — Get single message with delivery statuses
        app.MapGet("/messages/{id}", (string id, MessageService svc) =>
        {
            try { return Results.Ok(MapMessage(svc.GetMessage(id))); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new ErrorResponse(ex.Message)); }
        });

        // POST /messages/{id}/read — Mark message as read by recipient
        app.MapPost("/messages/{id}/read", (string id, MarkReadRequest req, DeliveryService svc) =>
        {
            try
            {
                svc.MarkAsRead(id, req.RecipientId);
                return Results.Ok();
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new ErrorResponse(ex.Message)); }
        });
    }

    // Delivery Worker

    private static void MapDeliveryRoutes(WebApplication app)
    {
        // POST /delivery/process — Trigger delivery worker (simulates background processing)
        app.MapPost("/delivery/process", (DeliveryService svc) =>
        {
            var processed = svc.ProcessPendingDeliveries();
            return Results.Ok(new ProcessDeliveriesResponse(processed.Count, processed));
        });
    }

    private static UserResponse MapUser(User u) =>
        new(u.Id, u.Name, u.CreatedAt);

    private static GroupResponse MapGroup(Group g) =>
        new(g.Id, g.Name, g.MemberIds, g.CreatedAt);

    private static MessageResponse MapMessage(Message m) =>
        new(m.Id, m.GroupId, m.SenderId, m.Text, m.CreatedAt,
            m.Status.ToString(),
            m.DeliveryStatuses.Select(ds =>
                new DeliveryStatusResponse(ds.RecipientId, ds.State.ToString(), ds.DeliveredAt, ds.ReadAt)
            ).ToList());
}
