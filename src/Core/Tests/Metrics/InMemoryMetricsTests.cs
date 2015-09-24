using System;
using System.Threading.Tasks;
using Foundatio.Extensions;
using Foundatio.Metrics;
using Foundatio.Tests.Utility;
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Foundatio.Tests.Metrics {
    public class InMemoryMetricsTests : CaptureTests {
        public InMemoryMetricsTests(CaptureFixture fixture, ITestOutputHelper output) : base(fixture, output) {}

        [Fact]
        public async Task CanIncrementCounter() {
            var metrics = new InMemoryMetricsClient();

            await metrics.CounterAsync("c1");
            Assert.Equal(1, metrics.GetCount("c1"));

            await metrics.CounterAsync("c1", 5);
            Assert.Equal(6, metrics.GetCount("c1"));

            var counter = metrics.Counters["c1"];
            Assert.True(counter.Rate > 400);

            await metrics.GaugeAsync("g1", 2.534);
            Assert.Equal(2.534, metrics.GetGaugeValue("g1"));

            await metrics.TimerAsync("t1", 50788);
            var stats = metrics.GetMetricStats();
            Assert.Equal(1, stats.Timings.Count);

            metrics.DisplayStats(_writer);
        }

#pragma warning disable 4014
        [Fact]
        public async Task CanWaitForCounter() {
            var metrics = new InMemoryMetricsClient();
            metrics.StartDisplayingStats(TimeSpan.FromMilliseconds(50), _writer);
            Task.Run(async () => {
                await Task.Delay(50);
                await metrics.CounterAsync("Test");
                await metrics.CounterAsync("Test");
            });

            var success = await metrics.WaitForCounterAsync("Test", 2, TimeSpan.FromMilliseconds(500));
            Assert.True(success);

            Task.Run(async () => {
                await Task.Delay(50);
                await metrics.CounterAsync("Test");
            });

            success = await metrics.WaitForCounterAsync("Test", timeout: TimeSpan.FromMilliseconds(500));
            Assert.True(success);

            success = await metrics.WaitForCounterAsync("Test", timeout: TimeSpan.FromMilliseconds(100));
            Assert.False(success);

            Task.Run(async () => {
                await Task.Delay(50);
                await metrics.CounterAsync("Test", 2);
            });

            success = await metrics.WaitForCounterAsync("Test", 2, TimeSpan.FromMilliseconds(500));
            Assert.True(success);

            success = await metrics.WaitForCounterAsync("Test", async () => await metrics.CounterAsync("Test"), cancellationToken: TimeSpan.FromMilliseconds(500).ToCancellationToken());
            Assert.True(success);

            Task.Run(async () => {
                await Task.Delay(50);
                await metrics.CounterAsync("Test");
            });

            success = await metrics.WaitForCounterAsync("Test", timeout: TimeSpan.FromMilliseconds(500));
            Assert.True(success);

            metrics.DisplayStats(_writer);
        }
#pragma warning restore 4014

        [Fact]
        public async Task CanDisplayStatsMultithreaded() {
            var metrics = new InMemoryMetricsClient();
            metrics.StartDisplayingStats(TimeSpan.FromMilliseconds(10), _writer);

            await Run.InParallel(100, async i => {
                await metrics.CounterAsync("Test");
                await Task.Delay(50);
            });
        }
    }
}