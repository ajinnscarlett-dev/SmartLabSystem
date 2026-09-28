using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartLab.Client;

internal static class ThumbnailRefreshPolicy
{
    internal record Candidate(int Id, bool Visible, DateTime LastAttempt);
    internal static int[] Select(IEnumerable<Candidate> candidates, DateTime now, int slots)
    {
        var due = candidates.Where(c => now - c.LastAttempt >= TimeSpan.FromSeconds(c.Visible ? 1 : 10)).ToList();
        // Reserve one slot for off-screen work to avoid starvation.
        var background = due.Where(c => !c.Visible).OrderBy(c => c.LastAttempt).Take(slots > 1 ? 1 : 0).ToList();
        return due.Except(background).OrderByDescending(c => c.Visible).ThenBy(c => c.LastAttempt)
            .Take(Math.Max(0, slots - background.Count)).Concat(background).Select(c => c.Id).ToArray();
    }
}
