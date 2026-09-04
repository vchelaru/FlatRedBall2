using System.Collections.Generic;
using FlatRedBall2.Collision;
using FlatRedBall2.Diagnostics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Diagnostics;

public class PerformanceMonitorTests
{
    private sealed class FakeRelationship : ICollisionRelationship
    {
        public FakeRelationship(string name, int deepCollisionCount, PartitionStatus partitionStatus, bool isEnabled = true)
        {
            DisplayName = name;
            DeepCollisionCount = deepCollisionCount;
            PartitionStatus = partitionStatus;
            IsEnabled = isEnabled;
        }

        public string DisplayName { get; }
        public int DeepCollisionCount { get; }
        public PartitionStatus PartitionStatus { get; }
        public bool IsEnabled { get; set; }
        public void RunCollisions() { }
    }

    private static readonly IReadOnlyList<ICollisionRelationship> NoRelationships = System.Array.Empty<ICollisionRelationship>();

    [Fact]
    public void Fps_KnownFrameTotalMsValues_ComputesCurrentMinAverageMax()
    {
        var monitor = new PerformanceMonitor { IsEnabled = true, WindowSize = 3 };

        // FrameTotalMs 10, 20, 25 -> FPS 100, 50, 40.
        monitor.Record(new FrameProfile { FrameTotalMs = 10 }, NoRelationships);
        monitor.Record(new FrameProfile { FrameTotalMs = 20 }, NoRelationships);
        monitor.Record(new FrameProfile { FrameTotalMs = 25 }, NoRelationships);

        var fps = monitor.Fps;
        fps.Current.ShouldBe(40.0, tolerance: 0.001);
        fps.Min.ShouldBe(40.0, tolerance: 0.001);
        fps.Max.ShouldBe(100.0, tolerance: 0.001);
        fps.Average.ShouldBe((100.0 + 50.0 + 40.0) / 3.0, tolerance: 0.001);
    }

    [Fact]
    public void GenerateReport_CoarseTimerResolution_WarnsAndNamesPlatform()
    {
        var monitor = new PerformanceMonitor { IsEnabled = true, PlatformLabel = "Firefox" };
        monitor.TimerResolutionMs = 1.0;

        monitor.Record(new FrameProfile { FrameTotalMs = 16 }, NoRelationships);

        var text = monitor.GenerateReport();
        text.ShouldContain("Platform: Firefox");
        text.ShouldContain("timer resolution 1.00ms");
        text.ShouldContain("per-phase timings below are unreliable");
        // Firefox is the coarsest common target, so the report names the specific remedy.
        text.ShouldContain("cross-origin isolated");
    }

    [Fact]
    public void GenerateReport_CollisionRelationships_OrdersBySeverityAndWarnsOnlyOnTheFixableRow()
    {
        var monitor = new PerformanceMonitor { IsEnabled = true };
        var relationships = new List<ICollisionRelationship>
        {
            // NotApplicable — a non-factory side, so no PartitionAxis can ever engage the sweep.
            new FakeRelationship("Player vs TileShapes", deepCollisionCount: 5, partitionStatus: PartitionStatus.NotApplicable),
            new FakeRelationship("Enemy vs Bullet", deepCollisionCount: 50, partitionStatus: PartitionStatus.Partitioned),
            new FakeRelationship("Enemy vs Pickup", deepCollisionCount: 20, partitionStatus: PartitionStatus.Unpartitioned)
        };

        monitor.Record(new FrameProfile { FrameTotalMs = 16 }, relationships);

        var report = monitor.GetCollisionReport();
        report[0].Name.ShouldBe("Enemy vs Bullet");
        report[0].DeepCollisionCount.ShouldBe(50);
        report[2].Name.ShouldBe("Player vs TileShapes");
        report[2].PartitionStatus.ShouldBe(PartitionStatus.NotApplicable);

        var text = monitor.GenerateReport();
        text.ShouldContain("Enemy vs Bullet: 50 deep checks [partitioned]");
        text.ShouldContain("Enemy vs Pickup: 20 deep checks [NOT PARTITIONED");
        text.ShouldContain("Player vs TileShapes: 5 deep checks [partitioning n/a");
        text.IndexOf("Enemy vs Bullet").ShouldBeLessThan(text.IndexOf("Enemy vs Pickup"));
    }

    [Fact]
    public void GenerateReport_DisabledRelationship_IsOmittedFromCollisionReport()
    {
        var monitor = new PerformanceMonitor { IsEnabled = true };
        var relationships = new List<ICollisionRelationship>
        {
            new FakeRelationship("Enemy vs Bullet", deepCollisionCount: 50, partitionStatus: PartitionStatus.Partitioned),
            new FakeRelationship("Player vs Door", deepCollisionCount: 5, partitionStatus: PartitionStatus.Unpartitioned, isEnabled: false)
        };

        monitor.Record(new FrameProfile { FrameTotalMs = 16 }, relationships);

        var report = monitor.GetCollisionReport();
        report.Count.ShouldBe(1);
        report[0].Name.ShouldBe("Enemy vs Bullet");
        monitor.GenerateReport().ShouldNotContain("Player vs Door");
    }

    [Fact]
    public void GenerateReport_FineTimerResolution_OmitsWarning()
    {
        var monitor = new PerformanceMonitor { IsEnabled = true, PlatformLabel = "Chrome" };
        monitor.TimerResolutionMs = 0.1;

        monitor.Record(new FrameProfile { FrameTotalMs = 16 }, NoRelationships);

        var text = monitor.GenerateReport();
        text.ShouldContain("Platform: Chrome");
        text.ShouldNotContain("unreliable");
    }

    [Fact]
    public void Record_IsEnabledFalse_DoesNotRecordFrame()
    {
        var monitor = new PerformanceMonitor { IsEnabled = false };

        monitor.Record(new FrameProfile { FrameTotalMs = 16 }, NoRelationships);

        monitor.FrameTotalMs.Current.ShouldBe(0);
        monitor.GetCollisionReport().ShouldBeEmpty();
    }

    [Fact]
    public void GpuStats_KnownFrameProfileValues_ComputesCurrentMinAverageMax()
    {
        var monitor = new PerformanceMonitor { IsEnabled = true, WindowSize = 2 };

        monitor.Record(new FrameProfile { DrawCallCount = 50, SpriteCount = 40, PrimitiveCount = 200, TextureCount = 10 }, NoRelationships);
        monitor.Record(new FrameProfile { DrawCallCount = 90, SpriteCount = 60, PrimitiveCount = 300, TextureCount = 12 }, NoRelationships);

        monitor.DrawCallCount.Current.ShouldBe(90);
        monitor.DrawCallCount.Min.ShouldBe(50);
        monitor.DrawCallCount.Max.ShouldBe(90);
        monitor.DrawCallCount.Average.ShouldBe(70);

        monitor.SpriteCount.Current.ShouldBe(60);
        monitor.PrimitiveCount.Current.ShouldBe(300);
        monitor.TextureCount.Current.ShouldBe(12);
    }

    [Fact]
    public void GenerateReport_IncludesGpuDrawCallStats()
    {
        var monitor = new PerformanceMonitor { IsEnabled = true };
        monitor.Record(new FrameProfile { FrameTotalMs = 16, DrawCallCount = 98, SpriteCount = 106, PrimitiveCount = 608, TextureCount = 132 }, NoRelationships);

        var text = monitor.GenerateReport();
        text.ShouldContain("DrawCalls");
        text.ShouldContain("98");
    }

    [Fact]
    public void Record_MoreFramesThanWindowSize_RingBufferRetainsOnlyMostRecent()
    {
        var monitor = new PerformanceMonitor { IsEnabled = true, WindowSize = 3 };

        // Window holds only the last 3: 3, 4, 5.
        for (int i = 1; i <= 5; i++)
            monitor.Record(new FrameProfile { FrameTotalMs = i }, NoRelationships);

        var stat = monitor.FrameTotalMs;
        stat.Current.ShouldBe(5);
        stat.Min.ShouldBe(3);
        stat.Max.ShouldBe(5);
        stat.Average.ShouldBe(4);
    }
}
