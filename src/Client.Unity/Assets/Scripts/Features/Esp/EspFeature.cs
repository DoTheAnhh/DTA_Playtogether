using DTA.Features.Base;
using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Features.Esp;

public sealed class EspCategorySettings
{
    public bool Enabled { get; set; } = true;
    public string HexColor { get; set; } = "#38bdf8";
    public float MaxDistance { get; set; } = 150f;
    public bool ShowDistance { get; set; } = true;
    public bool ShowLine { get; set; } = false;
}

public sealed class EspOptions
{
    public bool MasterEnabled { get; set; } = true;
    public EspCategorySettings Fish { get; set; } = new() { HexColor = "#38bdf8" };
    public EspCategorySettings Insect { get; set; } = new() { HexColor = "#4ade80" };
    public EspCategorySettings Ore { get; set; } = new() { HexColor = "#fbbf24" };
    public EspCategorySettings Relic { get; set; } = new() { HexColor = "#c084fc" };
    public EspCategorySettings Player { get; set; } = new() { HexColor = "#f43f5e", MaxDistance = 80f };
}

public sealed class EspStats
{
    public int TrackedEntitiesCount { get; set; }
    public int RenderedEntitiesCount { get; set; }
}

public readonly record struct ScreenPoint(float X, float Y, bool IsVisible);

public interface IWorldProjector
{
    ScreenPoint Project(Vector3 worldPos, Vector3 cameraPos, Quaternion cameraRot, ScreenMetrics metrics);
}

public sealed class WorldProjector : IWorldProjector
{
    public ScreenPoint Project(Vector3 worldPos, Vector3 cameraPos, Quaternion cameraRot, ScreenMetrics metrics)
    {
        // Vector projection math
        float dx = worldPos.X - cameraPos.X;
        float dy = worldPos.Y - cameraPos.Y;
        float dz = worldPos.Z - cameraPos.Z;

        // Simplified camera plane projection
        if (dz <= 0.1f) return new ScreenPoint(0, 0, false);

        float screenX = (metrics.Width * 0.5f) + ((dx / dz) * (metrics.Height * 0.5f));
        float screenY = (metrics.Height * 0.5f) - ((dy / dz) * (metrics.Height * 0.5f));

        bool onScreen = screenX >= 0 && screenX <= metrics.Width && screenY >= 0 && screenY <= metrics.Height;
        return new ScreenPoint(screenX, screenY, onScreen);
    }
}

public interface IEspService : IFeatureService
{
    EspOptions Options { get; }
    EspStats Stats { get; }
}

public sealed class EspService : IEspService
{
    public FeatureId Id => FeatureId.Esp;
    public string Name => "ESP & Radar";
    public bool IsRunning { get; private set; }

    public EspOptions Options { get; } = new();
    public EspStats Stats { get; } = new();

    public ValueTask<FeatureResult> StartAsync(CancellationToken ct = default)
    {
        IsRunning = true;
        return ValueTask.FromResult(FeatureResult.Ok("ESP active"));
    }

    public ValueTask<FeatureResult> StopAsync(CancellationToken ct = default)
    {
        IsRunning = false;
        return ValueTask.FromResult(FeatureResult.Ok("ESP stopped"));
    }
}
