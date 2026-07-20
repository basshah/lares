namespace Lares.Api.Services;

public class DeviceActionException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
