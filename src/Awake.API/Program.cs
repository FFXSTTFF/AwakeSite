using Awake.API.Extensions;
using Awake.API.Hubs;
using Awake.API.Middleware;
using Awake.API.Services;
using Awake.Application;
using Awake.Application.Common.Interfaces;
using Awake.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Application + Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 2. API
builder.Services.AddControllers();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddRateLimitingPolicies();
builder.Services.AddCorsPolicies(builder.Configuration);

// 3. SignalR
builder.Services.AddSignalR();
builder.Services.AddScoped<INotificationService, NotificationService>();

// 4. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 5. Serilog
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

var app = builder.Build();

// 6. Middleware pipeline (order matters)
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 7. Auto-register Discord slash commands on startup
await RegisterDiscordCommandsAsync(app);

app.Run();

// ── Discord command registration ─────────────────────────────────────────────

static async Task RegisterDiscordCommandsAsync(WebApplication app)
{
    var config = app.Services.GetRequiredService<IConfiguration>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    var botToken = config["Discord:BotToken"];
    var appId    = config["Discord:ApplicationId"];

    if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(appId))
    {
        logger.LogInformation("Discord credentials not configured — skipping command registration.");
        return;
    }

    try
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bot {botToken}");

        var commands = new object[]
        {
            new
            {
                name = "ticket",
                type = 1,
                description = "Submit an application to join clan Awake [LOVE] (direct modal)"
            },
            new
            {
                name = "szticket",
                type = 1,
                description = "Post the application button message in this channel"
            },
            new
            {
                name = "szticketadm",
                type = 1,
                description = "Set this channel as admin ticket feed",
                options = new[]
                {
                    new
                    {
                        name = "role",
                        description = "Officer/Admin role that gets access to ticket channels",
                        type = 8,
                        required = false
                    }
                }
            }
        };

        foreach (var cmd in commands)
        {
            var resp = await http.PostAsJsonAsync(
                $"https://discord.com/api/v10/applications/{appId}/commands", cmd);

            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("Failed to register Discord command: {Status}", resp.StatusCode);
        }

        logger.LogInformation("Discord slash commands registered successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to register Discord slash commands on startup.");
    }
}
