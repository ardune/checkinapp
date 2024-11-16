using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestionLogger.Persistence;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddTransient<QueueServiceClient>(b => new QueueServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage")));
    })
    .Build();

var queueServiceClient = host.Services.GetRequiredService<QueueServiceClient>();
await queueServiceClient.CreateQueueAsync(QueueNames.CheckDeliveryQueue);

host.Run();
