# Group Chat Messenger — Lab 2 (Variant 4)

A minimal but production-structured group chat backend built in **C# / ASP.NET Core 8**, implementing **Variant 4: Group Chat with Fan-out delivery**.

---

## Architecture

```
Client → HTTP API → Message Service → Database (SQLite)
                              ↓
                       Fan-Out Service → Delivery Queue (SQLite)
                              ↓
                      Delivery Service → Updates per-recipient status
```

This implements the **Fan-out on Write** strategy from ADR-001:
- When a message is sent, one delivery task is created **per recipient** in the queue
- The Delivery Service processes tasks asynchronously and updates per-recipient statuses
- Each recipient has independent tracking: `Pending → Enqueued → Delivered → Read`

### Message State Machine (from Lab 1)

```
Created → Stored → FanOutTriggered → DeliveryInProgress
       → PartiallyDelivered → FullyDelivered
       → PartiallyRead → FullyRead
```

---

## Project Structure

```
GroupChatMessenger/
├── Models/
│   ├── User.cs              # User entity
│   ├── Group.cs             # Group (conversation) entity
│   ├── Message.cs           # Message + DeliveryStatus + enums
│   └── DeliveryTask.cs      # Queue task model
├── Services/
│   ├── UserService.cs       # User CRUD + validation
│   ├── GroupService.cs      # Group management
│   ├── MessageService.cs    # Send orchestration
│   ├── FanOutService.cs     # Fan-out on write logic
│   └── DeliveryService.cs   # Delivery processing + read tracking
├── Storage/
│   └── Database.cs          # SQLite persistence layer
├── Api/
│   ├── Routes.cs            # Minimal API endpoint registration
│   └── Dtos.cs              # Request/response DTOs
├── Program.cs               # Entry point + DI setup
└── postman_collection.json  # Postman test collection

Tests/
└── GroupChatIntegrationTests.cs  # xUnit integration tests
```

---

## How to Run

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)

### Start the server

```bash
cd GroupChatMessenger
dotnet run
```

Server starts at `http://localhost:5000`.  
Swagger UI: `http://localhost:5000/swagger`

### Run tests

```bash
cd Tests
dotnet test
```

---

## API Reference

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/users` | Create a user |
| GET | `/users` | List all users |
| GET | `/users/{id}` | Get user by ID |
| POST | `/groups` | Create a group |
| GET | `/groups` | List all groups |
| GET | `/groups/{id}` | Get group by ID |
| POST | `/groups/{id}/members` | Add member to group |
| POST | `/groups/{id}/messages` | Send message to group → **202 Accepted** |
| GET | `/groups/{id}/messages` | Get message history |
| GET | `/messages/{id}` | Get message + delivery statuses |
| POST | `/messages/{id}/read` | Mark message as read by recipient |
| POST | `/delivery/process` | Process delivery queue (simulates worker) |

---

## Postman

Import `postman_collection.json` into Postman.

**Run order:**
1. Create User Alice → auto-saves `userId1`
2. Create User Bob → auto-saves `userId2`
3. Create User Charlie → auto-saves `userId3`
4. Create Group → auto-saves `groupId`
5. Send Message → auto-saves `messageId`
6. Process Deliveries
7. Mark as Read (Bob)
8. Get Single Message → check `deliveryStatuses`

---

## Features Implemented

- ✅ User creation and retrieval
- ✅ Group management (create, add members)
- ✅ Message persistence (SQLite)
- ✅ Unique message IDs (GUID), senderId, timestamp
- ✅ Error handling (empty message, unknown user/group, non-member)
- ✅ **Variant 4**: Fan-out on Write — per-recipient delivery tasks
- ✅ **Variant 4**: Per-recipient delivery status tracking
- ✅ Message state machine (7 states)
- ✅ Read receipt tracking per recipient
- ✅ Integration tests (5 scenarios)
- ✅ Postman collection with automated test scripts
- ✅ Swagger/OpenAPI documentation

---

## Defense Questions

**1. How does your system ensure messages are not lost?**  
Messages are written to SQLite before fan-out. Delivery tasks are also persisted in the DB, so if the server restarts, the queue survives and can be reprocessed.

**2. What happens if a recipient is offline?**  
The delivery task stays in the queue. The next call to `POST /delivery/process` will retry it. In production this would be a background `IHostedService`.

**3. How are messages uniquely identified?**  
Each message has a GUID `Id` generated at creation, plus `senderId` and `createdAt` timestamp.

**4. What errors may occur when sending?**  
- Empty message text → 400
- Unknown group → 404
- Unknown sender → 404
- Sender not a group member → 400

**5. How would this scale to 1 million users?**  
- Replace SQLite with PostgreSQL (horizontal reads via read replicas)
- Replace the in-DB queue with a real message broker (RabbitMQ / Kafka)
- Run multiple Delivery Service instances as competing consumers
- Add Redis for hot conversation caching
- Shard message storage by groupId
