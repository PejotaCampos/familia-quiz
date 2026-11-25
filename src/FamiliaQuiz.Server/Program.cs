using FamiliaQuiz.Server.Hubs;
using FamiliaQuiz.Server.Questions;
using FamiliaQuiz.Server.Rooms;
using FamiliaQuiz.Shared;

var builder = WebApplication.CreateBuilder(args);

// Render (and similar hosts) tell the app which port to listen on via $PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port)) builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RoomRegistry>();
builder.Services.AddSingleton<QuestionBank>();
builder.Services.AddSingleton<SpecialRoundBank>();
builder.Services.AddHostedService<IdleRoomCleanupService>();

builder.Services.AddSignalR(options =>
{
    // Background tabs (players in the video-call window) get throttled by the browser,
    // so be generous before declaring a client dead. Reconnect handles the rest.
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(90);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// The client is a static site on another origin (GitHub/Cloudflare Pages).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseCors();

// Cheap endpoint to wake the free-tier instance and check it is alive.
app.MapGet("/", () => "FamiliaQuiz server is running");
app.MapGet("/health", () => Results.Ok());

app.MapHub<GameHub>(HubRoutes.Game);

app.Run();
