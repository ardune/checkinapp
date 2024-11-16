using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace QuestionLogger;

public static class ForceSmsHttp
{
    [Function(nameof(ForceSmsHttp))]
    public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "test-sms")] HttpRequest req, FunctionContext context)
    {
        var log = context.GetLogger(nameof(ForceSmsHttp));
        log.LogInformation($"ForceMessage function executed at: {DateTime.UtcNow}");

        await SmsHelpers.SendMessage(log);

        return new OkResult();
    }
}