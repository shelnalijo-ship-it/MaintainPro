namespace MaintainPro.Application.MasterData;

public sealed record MasterDataWriteRequest(string Name, string? Description = null, bool IsActive = true);
public sealed record LocationWriteRequest(
    string Name, string? Description = null, Guid? DepartmentId = null, bool IsActive = true);
public sealed record MasterDataStatusRequest(bool IsActive);

public sealed record DepartmentDto(Guid Id, string Name, string? Description, bool IsActive);
public sealed record MachineCategoryDto(Guid Id, string Name, string? Description, bool IsActive);
public sealed record LocationDto(Guid Id, string Name, string? Description, Guid? DepartmentId, bool IsActive);
