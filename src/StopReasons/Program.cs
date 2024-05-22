using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers();

builder.Services.AddSingleton<StopReasons.Services.IMessageReceiver, StopReasons.Infra.RabbitMqMessageReceiver>(sp =>
    new StopReasons.Infra.RabbitMqMessageReceiver(builder.Configuration.GetSection("RabbitMqConfig").Get<StopReasons.Infra.RabbitMqConfiguration>()));

builder.Services.AddSingleton<StopReasons.Services.AvailabilityStateManager>(sp =>
    new StopReasons.Services.AvailabilityStateManager(builder.Configuration.GetSection("AvailabilityStateManagerConfig").Get<StopReasons.Services.AvailabilityStateManagerConfig>()));

builder.Services.AddHostedService<StopReasons.Jobs.AvailabilityMetricsListener>();

var app = builder.Build();

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

//app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
