using System.Windows.Forms;
using Casca.PrintAgent.Endpoints;

namespace Casca.PrintAgent;

internal static class Program
{
    // Solo el equipo local puede llamar al agente: el navegador de la caja corre en la misma PC.
    private const string AgentUrl = "http://localhost:9001";
    private const string SingleInstanceMutex = "Global\\CascaPrintAgent";

    [STAThread]
    private static int Main(string[] args)
    {
        // Una sola copia a la vez: si ya está abierto (p. ej. inicio automático + acceso directo), sale.
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutex, out var isFirstInstance);
        if (!isFirstInstance)
            return 0;

        Application.EnableVisualStyles();

        WebApplication app;
        try
        {
            app = BuildApp(args);
            app.StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo iniciar Casca Print Agent en {AgentUrl}.\n\n" +
                $"Verifica que el puerto 9001 no esté ocupado por otro programa.\n\n{ex.Message}",
                "Casca Print Agent",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        using (new TrayIcon(AgentUrl, Application.ExitThread))
        {
            Application.Run();
        }

        app.StopAsync().GetAwaiter().GetResult();
        return 0;
    }

    private static WebApplication BuildApp(string[] args)
    {
        // El agente puede arrancar desde el inicio de Windows con otra carpeta de trabajo,
        // por eso los archivos de configuración se buscan junto al ejecutable.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.WebHost.UseUrls(AgentUrl);
        builder.Services.AddPrintAgent();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        var app = builder.Build();

        // Chrome exige este encabezado cuando una página pública llama a un servidor local (Private Network Access).
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsOptions(context.Request.Method) &&
                context.Request.Headers.ContainsKey("Access-Control-Request-Private-Network"))
            {
                context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
            }

            await next(context);
        });

        app.UseWebSockets();
        app.UseWebSocketPrinterStatus(); // middleware antes del endpoint routing
        app.UseCors();

        app.MapGetPrinters();
        app.MapPrintTestPage();
        app.MapPrintSaleTicket();
        app.MapGetPrinterStatus();

        return app;
    }
}
