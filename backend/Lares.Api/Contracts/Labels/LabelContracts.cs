using System.ComponentModel.DataAnnotations;

namespace Lares.Api.Contracts.Labels;

public record CreateLabelRequest([Required, MaxLength(50)] string Name);

public record UpdateLabelRequest([Required, MaxLength(50)] string Name);

public record LabelDto(Guid Id, string Name);
