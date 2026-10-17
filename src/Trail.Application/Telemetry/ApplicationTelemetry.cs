using System.Diagnostics;

namespace Trail.Application.Telemetry;

public static class ApplicationTelemetry
{
    public const string Name = "Trail.Application";

    public static readonly ActivitySource ActivitySource = new(Name);
}
