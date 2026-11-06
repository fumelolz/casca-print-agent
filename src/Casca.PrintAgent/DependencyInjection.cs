using Casca.PrintAgent.Services;

namespace Casca.PrintAgent;

public static class DependencyInjection
{
    public static IServiceCollection AddPrintAgent(this IServiceCollection services)
    {
        services.AddSingleton<IPrinterService, PrinterService>();

        return services;
    }
}
