using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace QuestionLogger.Util;

public static class SmsHelper
{
    public static async Task<MessageResource> SendSms(string content)
    {
        InitClient();

        var toPhone = FunctionHelper.GetEnvironmentVariable("ToPhoneNumber");
        var fromPhone = FunctionHelper.GetEnvironmentVariable("FromPhoneNumber");
        var messageOptions = new CreateMessageOptions(new PhoneNumber(toPhone))
        {
            From = new PhoneNumber(fromPhone),
            Body = content
        };

        var messageResource = await MessageResource.CreateAsync(messageOptions);
        return messageResource;
    }

    public static async Task<bool> ShouldRetry(string messageSid, ILogger logger)
    {
        InitClient();
        var messageResource = await MessageResource.FetchAsync(messageSid);
        logger.LogInformation("Message {sid} had error code {code} with status {status}",
            messageResource?.Sid,
            messageResource?.ErrorCode,
            messageResource?.Status);
        return messageResource?.ErrorCode == 30005 && messageResource.DateCreated > DateTime.UtcNow.AddMinutes(-30);
    }

    private static void InitClient()
    {
        var accountSid = FunctionHelper.GetEnvironmentVariable("TwilioSid");
        var authToken = FunctionHelper.GetEnvironmentVariable("TwilioAuth");

        TwilioClient.Init(accountSid, authToken);
    }
}
