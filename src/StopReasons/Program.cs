using StopReasons.Infra;
using StopReasons.Config;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers();

builder.Services.AddSingleton<StopReasons.Services.IMessageReceiver, StopReasons.Infra.RabbitMqMessageReceiver>(sp =>
    new StopReasons.Infra.RabbitMqMessageReceiver(builder.Configuration.GetSection("RabbitMqConfig").Get<StopReasons.Infra.RabbitMqConfiguration>()));

builder.Services.AddSingleton<StopReasons.Services.AvailabilityStateManager>(sp => new StopReasons.Services.AvailabilityStateManager(
        config: builder.Configuration.GetSection("AvailabilityStateManagerConfig").Get<StopReasons.Services.AvailabilityStateManagerConfig>(),
        persistence: new PostgresBasedAvailabilityMetricStorage(config: builder.Configuration.GetSection("PostgresConfig").Get<StopReasons.Infra.PostgresConfig>())
    ));

builder.Services.AddHostedService<StopReasons.Jobs.AvailabilityMetricsListener>();

builder.Services.Configure<DowntimeReasonsConfig>(builder.Configuration.GetSection("DowntimeReasonsConfig"));

var app = builder.Build();

app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapControllers();

app.Run();
