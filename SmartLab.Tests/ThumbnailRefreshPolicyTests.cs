using SmartLab.Client;
using Xunit;
namespace SmartLab.Tests;

public sealed class ThumbnailRefreshPolicyTests
{
    [Fact]
    public void RespectsAvailableSlotsAndPrioritizesVisibleCardsWithoutStarvation()
    {
        var now = DateTime.UtcNow;
        var candidates = Enumerable.Range(1, 20).Select(id => new ThumbnailRefreshPolicy.Candidate(id, id <= 10, DateTime.MinValue));
        var selected = ThumbnailRefreshPolicy.Select(candidates, now, 4);
        Assert.Equal(4, selected.Length);
        Assert.Equal(3, selected.Count(id => id <= 10));
        Assert.Single(selected, id => id > 10);
        Assert.Empty(ThumbnailRefreshPolicy.Select(candidates, now, 0));
    }
    [Fact]
    public void HiddenCardsAreThrottledAndOlderVisibleCardsGoFirst()
    {
        var now = DateTime.UtcNow;
        var candidates = new[] {
            new ThumbnailRefreshPolicy.Candidate(1, false, now.AddSeconds(-5)),
            new ThumbnailRefreshPolicy.Candidate(2, true, now.AddSeconds(-2)),
            new ThumbnailRefreshPolicy.Candidate(3, true, now.AddSeconds(-8)) };
        Assert.Equal(new[] { 3, 2 }, ThumbnailRefreshPolicy.Select(candidates, now, 4));
    }
}
