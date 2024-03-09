using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace QuestionLogger;

public static class WebTrigger
{
    [FunctionName("GetQuestion")]
    public static async Task<IActionResult> GetSms([HttpTrigger(AuthorizationLevel.Function, "get", Route = "question")] HttpRequest req, ILogger log)
    {
        if (!IsAuthorized(req))
        {
            return new UnauthorizedResult();
        }

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
            if (difference < TimeSpan.FromMinutes(15))
            {
                return new ContentResult
                {
                    Content = JsonConvert.SerializeObject(new { value1 = question }),
                    ContentType = "application/json",
                    StatusCode = 200
                };
            }
        }

        return new NotFoundResult();
    }

    [FunctionName("SmsSent")]
    public static async Task<IActionResult> ReportSentSms([HttpTrigger(AuthorizationLevel.Function, "post", Route = "sms-out")] HttpRequest req, ILogger log)
    {
        if (!IsAuthorized(req))
        {
            return new UnauthorizedResult();
        }

        using var service = FunctionHelper.GetSheetsService();
        var ctNow = FunctionHelper.GetCentralTimeNow();
        var spreadsheetId = FunctionHelper.GetEnvironmentVariable("SheetId");

        var question = await new StreamReader(req.Body).ReadToEndAsync();

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

        return new OkResult();
    }

    [FunctionName("SmsResponse")]
    public static async Task<IActionResult> ReportResponseSms([HttpTrigger(AuthorizationLevel.Function, "post", Route = "sms-in")] HttpRequest req, ILogger log)
    {
        if (!IsAuthorized(req))
        {
            return new UnauthorizedResult();
        }

        string response = await new StreamReader(req.Body).ReadToEndAsync();

        log.LogInformation(response);
        return new OkResult();
    }

    [FunctionName("Test")]
    public static async Task<IActionResult> Test([HttpTrigger(AuthorizationLevel.Function, "get", Route = "test")] HttpRequest req, ILogger log)
    {
        var central = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        var result = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.UtcNow.UtcDateTime, central);

        return new OkResult();
    }

    private static bool IsAuthorized(HttpRequest req)
    {
        var header = req.Headers["API-KEY"];
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }
        var expectedKey = FunctionHelper.GetEnvironmentVariable("ApiKey");
        var key = RandomNumberGenerator.GetBytes(16);
        var a = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(header));
        var b = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(expectedKey));
        return a.SequenceEqual(b);

    }
}
