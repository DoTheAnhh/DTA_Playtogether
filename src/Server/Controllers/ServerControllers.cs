using Microsoft.AspNetCore.Mvc;
using DTA.Server.Services;
using DTA.Shared.Models;
using DTA.Shared.Protocol;

namespace DTA.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TeleportController : ControllerBase
{
    private readonly IServerTeleportService _teleportService;

    public TeleportController(IServerTeleportService teleportService)
    {
        _teleportService = teleportService;
    }

    [HttpGet("positions")]
    public ActionResult<ResponseEnvelope<IReadOnlyList<TelePositionDTO>>> GetAllPositions()
    {
        var positions = _teleportService.GetAllPositions();
        return Ok(ResponseEnvelope<IReadOnlyList<TelePositionDTO>>.Ok(positions, _teleportService.DataVersion));
    }

    [HttpGet("positions/{mapId:int}")]
    public ActionResult<ResponseEnvelope<IReadOnlyList<TelePositionDTO>>> GetPositionsByMap(int mapId)
    {
        var positions = _teleportService.GetPositionsByMap(mapId);
        return Ok(ResponseEnvelope<IReadOnlyList<TelePositionDTO>>.Ok(positions, _teleportService.DataVersion));
    }

    [HttpPost("positions")]
    public ActionResult<ResponseEnvelope<TelePositionDTO>> UpsertPosition([FromBody] TelePositionDTO position)
    {
        if (string.IsNullOrWhiteSpace(position.Name))
            return BadRequest(ResponseEnvelope<TelePositionDTO>.Error("INVALID_NAME", "Tên vị trí không được để trống"));

        var updated = _teleportService.UpsertPosition(position);
        return Ok(ResponseEnvelope<TelePositionDTO>.Ok(updated, _teleportService.DataVersion));
    }
}

[ApiController]
[Route("api/[controller]")]
public sealed class UIController : ControllerBase
{
    private readonly IServerUIService _uiService;

    public UIController(IServerUIService uiService)
    {
        _uiService = uiService;
    }

    [HttpGet("navigation")]
    public ActionResult<ResponseEnvelope<AppNavigationDTO>> GetNavigation()
    {
        var nav = _uiService.GetNavigation();
        return Ok(ResponseEnvelope<AppNavigationDTO>.Ok(nav));
    }

    [HttpGet("screens/{screenId}")]
    public ActionResult<ResponseEnvelope<UIScreenDTO>> GetScreen(string screenId)
    {
        var screen = _uiService.GetScreen(screenId);
        if (screen == null)
            return NotFound(ResponseEnvelope<UIScreenDTO>.Error("NOT_FOUND", $"Màn hình '{screenId}' không tồn tại"));

        return Ok(ResponseEnvelope<UIScreenDTO>.Ok(screen));
    }
}

public sealed class LicenseVerifyRequest
{
    public string Key { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
}

[ApiController]
[Route("api/[controller]")]
public sealed class LicenseController : ControllerBase
{
    private readonly IServerLicenseService _licenseService;

    public LicenseController(IServerLicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    [HttpPost("verify")]
    public ActionResult<ResponseEnvelope<LicenseDTO>> Verify([FromBody] LicenseVerifyRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Key))
            return BadRequest(ResponseEnvelope<LicenseDTO>.Error("INVALID_KEY", "Key không được để trống"));

        var lic = _licenseService.Verify(req.Key, req.HardwareId);
        return Ok(ResponseEnvelope<LicenseDTO>.Ok(lic));
    }
}
