using DTA.Server.Controllers;
using DTA.Server.Services;
using DTA.Shared.Models;
using DTA.Shared.Protocol;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DTA.Server.Tests;

public sealed class ServerTests
{
    [Fact]
    public void TeleportController_Returns_AuthoritativePositions_With_Rotation_AndViewpoint()
    {
        var teleService = new ServerTeleportService();
        var controller = new TeleportController(teleService);

        var actionResult = controller.GetAllPositions();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var envelope = Assert.IsType<ResponseEnvelope<IReadOnlyList<TelePositionDTO>>>(okResult.Value);

        Assert.Equal("ok", envelope.Status);
        Assert.NotNull(envelope.Payload);
        Assert.NotEmpty(envelope.Payload);

        // Every server teleport point MUST include Character Rotation and Character Viewpoint
        foreach (var pos in envelope.Payload)
        {
            Assert.False(string.IsNullOrEmpty(pos.Name));
            Assert.NotNull(pos.Viewpoint);
            Assert.True(pos.Viewpoint.CameraDistance > 0);
        }
    }

    [Fact]
    public void TeleportController_UpsertPosition_Increments_DataVersion()
    {
        var teleService = new ServerTeleportService();
        var controller = new TeleportController(teleService);
        int initialVersion = teleService.DataVersion;

        var newPos = new TelePositionDTO(
            "custom_1",
            "Bến phà mới",
            1001,
            "Plaza",
            10f, 2f, 30f,
            Quaternion.Identity,
            new CharacterViewpoint(45f, 10f, 4.5f, true)
        );

        var actionResult = controller.UpsertPosition(newPos);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var envelope = Assert.IsType<ResponseEnvelope<TelePositionDTO>>(okResult.Value);

        Assert.Equal("ok", envelope.Status);
        Assert.True(envelope.DataVersion > initialVersion);
    }

    [Fact]
    public void UIController_Returns_NavigationFeatures()
    {
        var uiService = new ServerUIService();
        var controller = new UIController(uiService);

        var actionResult = controller.GetNavigation();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var envelope = Assert.IsType<ResponseEnvelope<AppNavigationDTO>>(okResult.Value);

        Assert.Equal("ok", envelope.Status);
        Assert.NotNull(envelope.Payload);
        Assert.NotEmpty(envelope.Payload.Features);
    }

    [Fact]
    public void LicenseController_Verifies_ValidKey()
    {
        var licService = new ServerLicenseService();
        var controller = new LicenseController(licService);

        var actionResult = controller.Verify(new LicenseVerifyRequest { Key = "DTA-TEST-KEY", HardwareId = "HWID-12345" });
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var envelope = Assert.IsType<ResponseEnvelope<LicenseDTO>>(okResult.Value);

        Assert.Equal("ok", envelope.Status);
        Assert.NotNull(envelope.Payload);
        Assert.Equal("vip", envelope.Payload.Role);
        Assert.False(envelope.Payload.IsExpired);
    }
}
