using System.Reflection;

namespace Casca.PrintAgent.Endpoints;

public static class GetVersionEndpoint
{
    private static readonly string Version = ReadVersion();

    public static IEndpointRouteBuilder MapGetVersion(this IEndpointRouteBuilder app)
    {
        app.MapGet("/version", () => Results.Ok(new { version = Version }))
            .WithName("GetVersion")
            .WithSummary("Devuelve la versión instalada del agente.");

        return app;
    }

    private static string ReadVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return informational?.Split('+')[0] ?? "0.0.0";
    }
}
