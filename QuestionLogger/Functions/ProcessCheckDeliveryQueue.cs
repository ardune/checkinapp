using Microsoft.Azure.Functions.Worker;
using QuestionLogger.Persistence;
using QuestionLogger.Util;

namespace QuestionLogger.Functions;

public static class ProcessCheckDeliveryQueue
{
    [Function(nameof(ProcessCheckDeliveryQueue))]
    public static async Task Run([QueueTrigger(QueueNames.CheckDeliveryQueue)] CheckAndResendMessage message, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(ProcessCheckDeliveryQueue));
        var retry = await SmsHelper.ShouldRetry(message.PathSid, logger);
        if (!retry)
        {
            return;
        }

        await SmsHelper.SendSms(message.Body);
    }
}
