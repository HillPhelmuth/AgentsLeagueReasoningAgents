using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AgentsLeagueReasoningAgents.EmailFunction.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["ConnectionStrings:ReminderDb"]
        ?? throw new InvalidOperationException("Missing ConnectionStrings:ReminderDb setting.");

    return new CosmosClient(connectionString);
});

builder.Services.AddSingleton<ReminderRepository>();
builder.Services.AddSingleton<EmailDispatchService>();

builder.Build().Run();
