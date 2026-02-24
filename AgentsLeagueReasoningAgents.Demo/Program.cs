using AgentsLeagueReasoningAgents.Demo.Components;
using AgentsLeagueReasoningAgents.DependencyInjection;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using MSLearnPlatformClient.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddReasoningAgentsPreparation(builder.Configuration);
builder.Services.AddMsLearnCatalogClient();
var clientOptions = new CosmosClientOptions()
{
    UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    }
};
var cosmosClient = new CosmosClient(builder.Configuration["ConnectionStrings:ReminderDb"], clientOptions);
builder.Services.AddSingleton(cosmosClient);
var serviceBusClient = new ServiceBusClient(builder.Configuration["ConnectionStrings:ServiceBus"]);
builder.Services.AddSingleton(serviceBusClient);
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
