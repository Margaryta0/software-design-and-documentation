## 🔹 Variant 4 — Group Chat
**Focus:** scaling delivery logic

**Additional requirements:**
- Messages sent to multiple recipients
- Separate delivery status per recipient

**Key questions:**
- Fan-out strategy
- Performance implications
---

## Component Diagram:

```mermaid
graph LR
    Client --> API
    API --> Auth
    API --> MS
    MS --> DB
    MS --> FO
    FO --> DB
    FO --> Queue
    Queue --> DS
    DS --> DB
    DS --> Client
```
---



