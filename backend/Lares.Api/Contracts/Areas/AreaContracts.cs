using System.ComponentModel.DataAnnotations;

namespace Lares.Api.Contracts.Areas;

public record CreateAreaRequest([Required, MaxLength(100)] string Name);

public record UpdateAreaRequest([Required, MaxLength(100)] string Name);

public record AreaDto(Guid Id, string Name, int DeviceCount, DateTime CreatedAtUtc);
