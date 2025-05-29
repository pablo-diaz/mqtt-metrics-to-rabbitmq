using StopReasons.Jobs;
using StopReasons.Infra;
using StopReasons.Config;
using StopReasons.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers();

builder.Services.Configure<PostgresConfig>(config: builder.Configuration.GetSection("PostgresConfig"));
builder.Services.Configure<AvailabilityStateManagerConfig>(config: builder.Configuration.GetSection("AvailabilityStateManagerConfig"));
builder.Services.Configure<RabbitMqConfiguration>(config: builder.Configuration.GetSection("RabbitMqConfig"));
builder.Services.Configure<DowntimeReasonsConfig>(builder.Configuration.GetSection("DowntimeReasonsConfig"));

builder.Services.AddSingleton<IMessageReceiver, RabbitMqMessageReceiver>();
builder.Services.AddSingleton<IntegrationService>();
builder.Services.AddTransient<IAvailabilityMetricStorage, PostgresBasedAvailabilityMetricStorage>();
builder.Services.AddScoped<AvailabilityStateManager>();
builder.Services.AddSingleton<ServiceToFilterDevicesByLineOfBusiness>();

builder.Services.AddHostedService<AvailabilityMetricsListener>();

var app = builder.Build();

app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapControllers();

app.Run();
