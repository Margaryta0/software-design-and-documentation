## 🔹 Variant 4 — Group Chat
**Focus:** scaling delivery logic

**Additional requirements:**
- Messages sent to multiple recipients
- Separate delivery status per recipient

**Key questions:**
- Fan-out strategy
- Performance implications
---

## Part 1 - Component Diagram:

```mermaid
graph LR
    Client --> API
    API --> Auth
    API --> MessageService
    MessageService --> DB[(Messages DB)]
    MessageService --> Fan-OutService
    Fan-OutService --> DB[(Messages DB)]
    Fan-OutService --> Queue
    Queue --> DeliveryService
    DeliveryService --> DB[(Messages DB)]
    DeliveryService --> Client
```
---
## Part 2 — Sequence Diagram:

```mermaid
sequenceDiagram
    participant A
    participant Client
    participant API
    participant MS as Message Service
    participant DB
    participant FO as Fan-out Service
    participant Queue
    participant DS as Delivery Service
    participant B as Client B

    A->>Client: Send message
    Client->>API: POST /groups/{id}/messages
    API->>MS: createMessage()
    MS->>DB: save(message)
    MS->>FO: fanOut(messageId)
    FO->>DB: getGroupMembers()
    FO->>Queue: enqueue(messageId, recipientId)
    API-->>Client: 202 Accepted

    Queue->>DS: deliver(messageId, B)
    DS->>B: WebSocket delivery
    DS->>DB: update status = delivered
```
---
## Part 3 — State Diagram(Message):

```mermaid
Created
→ Stored
→ FanOutTriggered
→ DeliveryInProgress
→ FullyDelivered
→ FullyRead
```
---
## Part 4 — ADR

```markdown
# ADR-001: Fan-out on Write for Group Chat

## Status
Accepted

## Context
Group messages must be delivered to multiple recipients.
Each recipient requires independent delivery tracking.
Users may be online or offline.

## Decision
Use asynchronous Fan-out on Write.
After saving a message, the system retrieves group members
and enqueues one delivery task per recipient.

## Alternatives

### Fan-out on Read
Rejected — does not support real-time delivery well.

### Direct WebSocket broadcast
Rejected — does not support offline users or retries.

## Consequences

+ Independent delivery tracking
+ Reliable asynchronous delivery
+ Scales with worker count

- More delivery tasks for large groups
- Requires queue monitoring
```

---



