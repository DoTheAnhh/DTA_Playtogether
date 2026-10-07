using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DTA.Server.Services;

namespace DTA.Server;

public static class ServerHost
{
    public static WebApplication BuildServer(string[]? args = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? []);
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddSingleton<IServerTeleportService, ServerTeleportService>();
        builder.Services.AddSingleton<IServerUIService, ServerUIService>();
        builder.Services.AddSingleton<IServerLicenseService, ServerLicenseService>();

        var app = builder.Build();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapControllers();
        return app;
    }
}
