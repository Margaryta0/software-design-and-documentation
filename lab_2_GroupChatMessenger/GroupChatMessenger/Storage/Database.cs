using Microsoft.Data.Sqlite;
using GroupChatMessenger.Models;
using System.Text.Json;

namespace GroupChatMessenger.Storage;

public class Database : IDisposable
{
    private readonly SqliteConnection _connection;

    public Database(string connectionString = "Data Source=messenger.db")
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Groups (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                MemberIds TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Messages (
                Id TEXT PRIMARY KEY,
                GroupId TEXT NOT NULL,
                SenderId TEXT NOT NULL,
                Text TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                Status INTEGER NOT NULL,
                DeliveryStatuses TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS DeliveryTasks (
                Id TEXT PRIMARY KEY,
                MessageId TEXT NOT NULL,
                RecipientId TEXT NOT NULL,
                EnqueuedAt TEXT NOT NULL,
                RetryCount INTEGER NOT NULL DEFAULT 0
            );
        ";
        cmd.ExecuteNonQuery();
    }

    // --- Users ---

    public void SaveUser(User user)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO Users (Id, Name, CreatedAt)
            VALUES ($id, $name, $createdAt)";
        cmd.Parameters.AddWithValue("$id", user.Id);
        cmd.Parameters.AddWithValue("$name", user.Name);
        cmd.Parameters.AddWithValue("$createdAt", user.CreatedAt.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public User? GetUser(string id)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, CreatedAt FROM Users WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new User
        {
            Id = reader.GetString(0),
            Name = reader.GetString(1),
            CreatedAt = DateTime.Parse(reader.GetString(2))
        };
    }

    public List<User> GetAllUsers()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, CreatedAt FROM Users";
        var users = new List<User>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new User
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                CreatedAt = DateTime.Parse(reader.GetString(2))
            });
        }
        return users;
    }

    // --- Groups ---

    public void SaveGroup(Group group)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO Groups (Id, Name, MemberIds, CreatedAt)
            VALUES ($id, $name, $memberIds, $createdAt)";
        cmd.Parameters.AddWithValue("$id", group.Id);
        cmd.Parameters.AddWithValue("$name", group.Name);
        cmd.Parameters.AddWithValue("$memberIds", JsonSerializer.Serialize(group.MemberIds));
        cmd.Parameters.AddWithValue("$createdAt", group.CreatedAt.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public Group? GetGroup(string id)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, MemberIds, CreatedAt FROM Groups WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new Group
        {
            Id = reader.GetString(0),
            Name = reader.GetString(1),
            MemberIds = JsonSerializer.Deserialize<List<string>>(reader.GetString(2)) ?? new(),
            CreatedAt = DateTime.Parse(reader.GetString(3))
        };
    }

    public List<Group> GetAllGroups()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, MemberIds, CreatedAt FROM Groups";
        var groups = new List<Group>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            groups.Add(new Group
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                MemberIds = JsonSerializer.Deserialize<List<string>>(reader.GetString(2)) ?? new(),
                CreatedAt = DateTime.Parse(reader.GetString(3))
            });
        }
        return groups;
    }

    // --- Messages ---

    public void SaveMessage(Message message)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO Messages (Id, GroupId, SenderId, Text, CreatedAt, Status, DeliveryStatuses)
            VALUES ($id, $groupId, $senderId, $text, $createdAt, $status, $deliveryStatuses)";
        cmd.Parameters.AddWithValue("$id", message.Id);
        cmd.Parameters.AddWithValue("$groupId", message.GroupId);
        cmd.Parameters.AddWithValue("$senderId", message.SenderId);
        cmd.Parameters.AddWithValue("$text", message.Text);
        cmd.Parameters.AddWithValue("$createdAt", message.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$status", (int)message.Status);
        cmd.Parameters.AddWithValue("$deliveryStatuses", JsonSerializer.Serialize(message.DeliveryStatuses));
        cmd.ExecuteNonQuery();
    }

    public Message? GetMessage(string id)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, GroupId, SenderId, Text, CreatedAt, Status, DeliveryStatuses FROM Messages WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadMessage(reader);
    }

    public List<Message> GetGroupMessages(string groupId)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, GroupId, SenderId, Text, CreatedAt, Status, DeliveryStatuses
            FROM Messages WHERE GroupId = $groupId
            ORDER BY CreatedAt ASC";
        cmd.Parameters.AddWithValue("$groupId", groupId);
        var messages = new List<Message>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) messages.Add(ReadMessage(reader));
        return messages;
    }

    private static Message ReadMessage(SqliteDataReader r) => new()
    {
        Id = r.GetString(0),
        GroupId = r.GetString(1),
        SenderId = r.GetString(2),
        Text = r.GetString(3),
        CreatedAt = DateTime.Parse(r.GetString(4)),
        Status = (MessageStatus)r.GetInt32(5),
        DeliveryStatuses = JsonSerializer.Deserialize<List<DeliveryStatus>>(r.GetString(6)) ?? new()
    };

    // --- Delivery Queue ---

    public void EnqueueDeliveryTask(DeliveryTask task)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO DeliveryTasks (Id, MessageId, RecipientId, EnqueuedAt, RetryCount)
            VALUES ($id, $messageId, $recipientId, $enqueuedAt, $retryCount)";
        cmd.Parameters.AddWithValue("$id", task.Id);
        cmd.Parameters.AddWithValue("$messageId", task.MessageId);
        cmd.Parameters.AddWithValue("$recipientId", task.RecipientId);
        cmd.Parameters.AddWithValue("$enqueuedAt", task.EnqueuedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$retryCount", task.RetryCount);
        cmd.ExecuteNonQuery();
    }

    public List<DeliveryTask> DequeuePendingTasks(int limit = 10)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, MessageId, RecipientId, EnqueuedAt, RetryCount
            FROM DeliveryTasks
            ORDER BY EnqueuedAt ASC
            LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        var tasks = new List<DeliveryTask>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tasks.Add(new DeliveryTask
            {
                Id = reader.GetString(0),
                MessageId = reader.GetString(1),
                RecipientId = reader.GetString(2),
                EnqueuedAt = DateTime.Parse(reader.GetString(3)),
                RetryCount = reader.GetInt32(4)
            });
        }
        return tasks;
    }

    public void DeleteDeliveryTask(string taskId)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM DeliveryTasks WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", taskId);
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
