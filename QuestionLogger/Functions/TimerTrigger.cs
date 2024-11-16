using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using QuestionLogger.Persistence;
using QuestionLogger.Util;

namespace QuestionLogger.Functions;

public class SendMessageTimer(QueueServiceClient queueServiceClient)
{
    [Function(nameof(SendMessageTimer))]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo myTimer, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(SendMessageTimer));
        logger.LogInformation($"C# Timer trigger function executed at: {DateTime.UtcNow}");

        var question = await SheetsHelper.GetQuestion();
        if (string.IsNullOrWhiteSpace(question))
        {
            logger.LogInformation("No question");
            return;
        }

        logger.LogInformation("question: " + question);

        var message = await SmsHelper.SendSms(question);

        await SheetsHelper.LogQuestion(question);

        var queueClient = queueServiceClient.GetQueueClient(QueueNames.CheckDeliveryQueue);
        await queueClient.SendMessageAsync(JsonSerializer.Serialize(new CheckAndResendMessage
        {
            PathSid = message.Sid,
            Body = message.Body
        }), visibilityTimeout: TimeSpan.FromMinutes(1));

        logger.LogInformation("C# Timer trigger function executed at: {message}", message);
    }
}
