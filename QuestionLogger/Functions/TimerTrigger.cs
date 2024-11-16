using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using QuestionLogger.Util;

namespace QuestionLogger.Functions;

public static class SendMessageTimer
{
    [Function(nameof(SendMessageTimer))]
    public static async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo myTimer, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(SendMessageTimer));
        logger.LogInformation($"C# Timer trigger function executed at: {DateTime.UtcNow}");

        var message = await SmsHelpers.SendMessage(logger);
        logger.LogInformation("C# Timer trigger function executed at: {message}", message);

    }
}
