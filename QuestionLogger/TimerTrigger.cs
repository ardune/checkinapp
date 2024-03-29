using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace QuestionLogger;

public static class TimerTrigger
{
    [FunctionName("TimerTrigger")]
    public static async Task RunAsync([TimerTrigger("0 0 * * * *")] TimerInfo myTimer, ILogger log)
    {
        log.LogInformation($"C# Timer trigger function executed at: {DateTime.UtcNow}");

        var question = await GetQuestion();
        if (string.IsNullOrWhiteSpace(question))
        {
            log.LogInformation("No question");
            return;
        }

        log.LogInformation("question: " + question);

        var accountSid = FunctionHelper.GetEnvironmentVariable("TwilioSid");
        var authToken = FunctionHelper.GetEnvironmentVariable("TwilioAuth");
        var toPhone = FunctionHelper.GetEnvironmentVariable("ToPhoneNumber");
        var fromPhone = FunctionHelper.GetEnvironmentVariable("FromPhoneNumber");

        TwilioClient.Init(accountSid, authToken);

        var messageOptions = new CreateMessageOptions(new PhoneNumber(toPhone))
        {
            From = new PhoneNumber(fromPhone),
            Body = question
        };

        await MessageResource.CreateAsync(messageOptions);

        using var service = FunctionHelper.GetSheetsService();
        var spreadsheetId = FunctionHelper.GetEnvironmentVariable("SheetId");

        var ctNow = FunctionHelper.GetCentralTimeNow();
        var values = new List<IList<object>>
        {
            new List<object>
            {
                question, ctNow.ToString(FunctionHelper.DateFormat), "-", "-"
            }
        };
        var updateRequest = service.Spreadsheets.Values.Append(new ValueRange
        {
            Values = values,
        }, spreadsheetId, "Logs!A:D");
        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

        await updateRequest.ExecuteAsync();
    }

    private static async Task<string> GetQuestion()
    {
        using var service = FunctionHelper.GetSheetsService();
        var ctNow = FunctionHelper.GetCentralTimeNow();
        var spreadsheetId = FunctionHelper.GetEnvironmentVariable("SheetId");

        var getData = await service.Spreadsheets.Values.Get(spreadsheetId, "Questions!A2:B30").ExecuteAsync();
        foreach (var row in getData.Values)
        {
            var time = row[0].ToString();
            var question = row[1].ToString();
            if (!TimeOnly.TryParse(time, out var parsed))
            {
                continue;
            }

            var timespan = parsed.ToTimeSpan();
            var difference = (timespan - ctNow.TimeOfDay).Duration();
            if (difference < TimeSpan.FromMinutes(60 / 2.0))
            {
                return question;
            }
        }

        return null;
    }
}
