using System.Collections.Concurrent;
using System.Reflection;
using SmartLab.Server.Controllers;

namespace SmartLab.Server;

internal static class RemoteControlSessionTracker
{
    private static readonly ConcurrentDictionary<int, DateTime> LastSeenUtc = new();

    public static void Touch(int pcId, int userId)
    {
        if (!TryGetSessions(out ConcurrentDictionary<int, int>? sessions))
            return;

        if (sessions.TryGetValue(pcId, out int ownerUserId) && ownerUserId == userId)
            LastSeenUtc[pcId] = DateTime.UtcNow;
    }

    public static void CleanupExpired(TimeSpan ttl, ISet<int>? offlinePcIds = null)
    {
        if (!TryGetSessions(out ConcurrentDictionary<int, int>? sessions))
            return;

        DateTime cutoff = DateTime.UtcNow - ttl;

        foreach (var pair in LastSeenUtc)
        {
            bool expired = pair.Value < cutoff;
            bool offline = offlinePcIds?.Contains(pair.Key) == true;

            if (!expired && !offline)
                continue;

            sessions.TryRemove(pair.Key, out _);
            LastSeenUtc.TryRemove(pair.Key, out _);
        }

        // A session can predate the tracker (for example immediately after a
        // server restart) and therefore have no timestamp. Fail safe by removing
        // any untracked session only when the owning PC is offline; active online
        // sessions are allowed to establish their lease on the next frame request.
        if (offlinePcIds == null)
            return;

        foreach (int pcId in offlinePcIds)
        {
            sessions.TryRemove(pcId, out _);
            LastSeenUtc.TryRemove(pcId, out _);
        }
    }

    public static void Remove(int pcId)
    {
        if (TryGetSessions(out ConcurrentDictionary<int, int>? sessions))
            sessions.TryRemove(pcId, out _);

        LastSeenUtc.TryRemove(pcId, out _);
    }

    private static bool TryGetSessions(out ConcurrentDictionary<int, int>? sessions)
    {
        sessions = null;

        FieldInfo? field = typeof(PCCommandController).GetField(
            "RemoteControlSessions",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (field?.GetValue(null) is ConcurrentDictionary<int, int> value)
        {
            sessions = value;
            return true;
        }

        return false;
    }
}
