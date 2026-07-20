using System.ComponentModel.DataAnnotations;
using Lares.Api.Contracts.Labels;
using Lares.Api.Domain;

namespace Lares.Api.Contracts.Devices;

public record CreateDeviceRequest(
    [Required, MaxLength(100)] string Name,
    [Required] DeviceType Type,
    Guid? AreaId);

public record UpdateDeviceRequest(
    [Required, MaxLength(100)] string Name,
    Guid? AreaId,
    IReadOnlyList<Guid> LabelIds,
    DeviceAttributesDto Attributes);

// Domain-in nullable-qrup formasını birbaşa güzgüləyir; leaf attribute class-ları Domain-dən
// təkrar istifadə olunur (EF-ə xas davranışları yoxdur, sadəcə data daşıyıcılarıdır).
public record DeviceAttributesDto(
    LightAttributes? Light,
    SocketAttributes? Socket,
    ThermostatAttributes? Thermostat,
    CameraAttributes? Camera,
    TvAttributes? Tv);

public record DeviceDto(
    Guid Id,
    string Name,
    string Type,
    Guid? AreaId,
    string? AreaName,
    string State,
    DeviceAttributesDto Attributes,
    IReadOnlyList<LabelDto> Labels,
    DateTime CreatedAtUtc);
