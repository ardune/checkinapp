using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using QuestionLogger.Persistence;
using QuestionLogger.Util;

namespace QuestionLogger.Functions;

public class ForceSmsHttp(QueueServiceClient queueServiceClient)
{
    [Function(nameof(ForceSmsHttp))]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "test-sms")] HttpRequest req, FunctionContext context)
    {
        var log = context.GetLogger(nameof(ForceSmsHttp));
        log.LogInformation($"ForceMessage function executed at: {DateTime.UtcNow}");

        var queueClient = queueServiceClient.GetQueueClient(QueueNames.CheckDeliveryQueue);
        var message = await SmsHelper.SendSms("testing only");
        await queueClient.SendMessageAsync(JsonSerializer.Serialize(new CheckAndResendMessage
        {
            PathSid = message.Sid,
            Body = message.Body
        }), visibilityTimeout: TimeSpan.FromMinutes(1));

        return new OkResult();
    }
}
