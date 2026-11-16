using FreeHarbor.Core.Agent;
using Xunit;

namespace FreeHarbor.Preflight.Tests;

/// <summary>The one collection that runs with nothing else beside it (DD363).</summary>
/// <remarks>
/// For a test that reads a clock. xUnit runs a collection that disables parallelization after the
/// others and on its own, so a ratio of two timings is not moved by a thousand tests sharing the
/// machine. Join it only with a test that times something.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AloneCollection
{
    /// <summary>The collection name, so no class spells the string twice.</summary>
    public const string Name = "alone";
}

/// <summary>What a follow costs as its input grows (DD253), measured alone (DD363).</summary>
[Collection(AloneCollection.Name)]
public sealed class AgentFollowCostTests
{
    [Fact]
    public void A_follow_costs_what_it_reads_rather_than_the_square_of_it()
    {
        // `--out` lifts the ceiling on purpose, so the only bounds left are the deadline and the
        // pattern. Re-splitting the whole buffer per chunk spent the deadline splitting; four times
        // the input has to cost about four times the time, not sixteen.
        //
        // DD363: this failed in full runs and passed alone. A single sample of a few milliseconds,
        // taken while the rest of the suite shared the machine, could be inflated in either half.
        // So it runs in a collection of its own, and each size is the least of five samples.
        static TimeSpan Cost(int lines)
        {
            var body = Enumerable.Range(0, lines)
                .Select(i => $"2024-01-01T00:00:00.000000000Z line {i} of a service that does not stop\n")
                .SelectMany(l => AgentFollowTests.Frame(1, l))
                .ToArray();

            // Warm first, so the measurement is the work rather than the first-call JIT.
            Follow(body);

            var least = TimeSpan.MaxValue;
            for (var sample = 0; sample < 5; sample++)
            {
                var started = System.Diagnostics.Stopwatch.StartNew();
                Follow(body);
                least = started.Elapsed < least ? started.Elapsed : least;
            }

            return least;
        }

        var small = Cost(4_000);
        var large = Cost(16_000);

        // Linear lands near 4x, quadratic near 16x; the bar is slack and still separates the two.
        var ratio = large.TotalMilliseconds / Math.Max(small.TotalMilliseconds, 1);
        Assert.True(ratio < 9, $"four times the input cost {ratio:F1} times the time, which is not linear.");
    }

    private static void Follow(byte[] body) => AgentFollowTests.Followed(
        new MemoryStream(body), new LogQuery(), "never arrives", AgentFollowTests.Patience, ceiling: false);
}
