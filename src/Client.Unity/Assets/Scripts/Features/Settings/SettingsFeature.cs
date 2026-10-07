using DTA.Features.Base;
using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Features.Settings;

public interface IRiskConsentGuard
{
    bool IsTeleportRiskAccepted { get; set; }
    bool IsMemoryPatchRiskAccepted { get; set; }
    bool CanExecuteHighRiskAction();
}

public sealed class RiskConsentGuard : IRiskConsentGuard
{
    public bool IsTeleportRiskAccepted { get; set; } = true;
    public bool IsMemoryPatchRiskAccepted { get; set; } = true;

    public bool CanExecuteHighRiskAction() => IsTeleportRiskAccepted && IsMemoryPatchRiskAccepted;
}

public interface ISettingsService : IFeatureService
{
    LicenseDTO? CurrentLicense { get; }
    IRiskConsentGuard RiskGuard { get; }
    bool ValidateLicense(string key, string hwid);
}

public sealed class SettingsService : ISettingsService
{
    public FeatureId Id => FeatureId.Settings;
    public string Name => "Cài đặt & Bảo mật";
    public bool IsRunning { get; private set; } = true;

    public LicenseDTO? CurrentLicense { get; private set; }
    public IRiskConsentGuard RiskGuard { get; } = new RiskConsentGuard();

    public ValueTask<FeatureResult> StartAsync(CancellationToken ct = default)
    {
        IsRunning = true;
        return ValueTask.FromResult(FeatureResult.Ok("Settings active"));
    }

    public ValueTask<FeatureResult> StopAsync(CancellationToken ct = default)
    {
        IsRunning = false;
        return ValueTask.FromResult(FeatureResult.Ok("Settings stopped"));
    }

    public bool ValidateLicense(string key, string hwid)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        CurrentLicense = new LicenseDTO
        {
            Key = key,
            HardwareId = hwid,
            Role = "vip",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
            EnabledFeatures = ["Fishing", "Mining", "Insect", "Excavation", "Farm", "Collect", "Teleport", "Esp"]
        };
        return true;
    }
}
