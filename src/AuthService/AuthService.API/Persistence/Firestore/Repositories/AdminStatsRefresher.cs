namespace AuthService.API.Persistence.Firestore.Repositories;

public sealed class AdminStatsRefresher : IAdminStatsRefresher
{
    private readonly IUserRepository _users;
    private readonly ILoginEventRepository _loginEvents;
    private readonly IAdminStatsRepository _adminStats;

    public AdminStatsRefresher(
        IUserRepository users,
        ILoginEventRepository loginEvents,
        IAdminStatsRepository adminStats)
    {
        _users = users;
        _loginEvents = loginEvents;
        _adminStats = adminStats;
    }

    public async Task<AdminStatsSnapshot> RefreshAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var last24Hours = now.AddHours(-24);

        var counts = await _users.GetUserCountsAsync(ct);
        var events = await _loginEvents.GetEventsSinceAsync(last24Hours, ct);

        var totalEvents = events.Count;
        var failedEvents = 0;
        var flaggedEvents = 0;
        var distinctIps = new HashSet<string>();

        foreach (var ev in events)
        {
            if (!ev.Success)
            {
                failedEvents++;
            }

            if (ev.IsFlagged)
            {
                flaggedEvents++;
            }

            if (!string.IsNullOrWhiteSpace(ev.IpAddress))
            {
                distinctIps.Add(ev.IpAddress);
            }
        }

        var aiRiskScore = ComputeRiskScore(totalEvents, failedEvents, flaggedEvents, distinctIps.Count);
        var aiRiskLevel = DetermineRiskLevel(aiRiskScore);

        var snapshot = new AdminStatsSnapshot(
            TotalUsers: counts.TotalUsers,
            ActiveUsers: counts.ActiveUsers,
            SuspendedUsers: counts.SuspendedUsers,
            DeletedUsers: counts.DeletedUsers,
            TotalLoginEventsLast24Hours: totalEvents,
            FailedLoginEventsLast24Hours: failedEvents,
            FlaggedEventsLast24Hours: flaggedEvents,
            DistinctSourceIpsLast24Hours: distinctIps.Count,
            AiRiskScore: aiRiskScore,
            AiRiskLevel: aiRiskLevel,
            ComputedAt: now);

        await _adminStats.UpsertAsync(snapshot, ct);
        return snapshot;
    }

    private static int ComputeRiskScore(int totalEvents, int failedEvents, int flaggedEvents, int distinctIps)
    {
        var score = (failedEvents * 10) + (flaggedEvents * 20) + (distinctIps * 3);

        if (totalEvents >= 25)
        {
            score += 10;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static string DetermineRiskLevel(int score)
    {
        if (score >= 70)
        {
            return "High";
        }

        if (score >= 40)
        {
            return "Medium";
        }

        return "Low";
    }
}
