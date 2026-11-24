namespace FamiliaQuiz.Server.Rooms;

/// <summary>Periodically frees memory used by abandoned rooms.</summary>
public sealed class IdleRoomCleanupService(RoomRegistry rooms, ILogger<IdleRoomCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MaxIdle = TimeSpan.FromHours(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var removed = rooms.RemoveIdleRooms(MaxIdle);
            if (removed > 0) logger.LogInformation("Removed {Count} idle room(s)", removed);
        }
    }
}
