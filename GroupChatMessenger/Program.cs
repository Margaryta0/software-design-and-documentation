using GroupChatMessenger.Storage;
using GroupChatMessenger.Services;
using GroupChatMessenger.Api;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Group Chat Messenger API", Version = "v1" });
});

var dbPath = builder.Configuration.GetValue<string>("DatabasePath") ?? "Data Source=messenger.db";
builder.Services.AddSingleton(new Database(dbPath));

builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<GroupService>();
builder.Services.AddSingleton<FanOutService>();
builder.Services.AddSingleton<DeliveryService>();
builder.Services.AddSingleton<MessageService>();


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

Routes.MapRoutes(app);

app.Run();

public partial class Program { }
