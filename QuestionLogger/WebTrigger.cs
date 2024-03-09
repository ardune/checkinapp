using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
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

        string answerToQuestion = await new StreamReader(req.Body).ReadToEndAsync();

        using var service = FunctionHelper.GetSheetsService();
        var ctNow = FunctionHelper.GetCentralTimeNow();
        var spreadsheetId = FunctionHelper.GetEnvironmentVariable("SheetId");

        var emptyUpdateRequest = service.Spreadsheets.Values.Append(new ValueRange
        {
            Values = new List<IList<object>>
            {
                new List<object>()
            },
        }, spreadsheetId, "Logs!A:D");
        emptyUpdateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

        var emptyUpdateResult = await emptyUpdateRequest.ExecuteAsync();
        // when update is used without any data - the result points to the cell that would have been appended to
        var rowNumber = int.Parse(Regex.Replace(emptyUpdateResult.Updates.UpdatedRange, "[^0-9]", string.Empty));

        var sheetRef = await service.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
        var sheetId = sheetRef.Sheets.Single(x => x.Properties.Title == "Logs");
        await service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request>
            {
                new()
                {
                    UpdateCells = new UpdateCellsRequest
                    {
                        Fields = "*",
                        Start = new GridCoordinate
                        {
                            ColumnIndex = 2,
                            // row number - 2 because 0-index and we want last row with data
                            RowIndex = rowNumber - 2,
                            SheetId = sheetId.Properties.SheetId
                        },
                        Rows = new List<RowData>
                        {
                            new()
                            {
                                Values = new List<CellData>
                                {
                                    new()
                                    {
                                        UserEnteredValue = new ExtendedValue
                                        {
                                            StringValue = answerToQuestion,
                                        },
                                    },
                                    new()
                                    {
                                        UserEnteredValue = new ExtendedValue
                                        {
                                            StringValue = ctNow.ToString(FunctionHelper.DateFormat),
                                        },
                                    },
                                }
                            }
                        }
                    }
                }
            }
        }, spreadsheetId).ExecuteAsync();

        await emptyUpdateRequest.ExecuteAsync();
        return new OkResult();
    }

    [FunctionName("Test")]
    public static Task<IActionResult> Test([HttpTrigger(AuthorizationLevel.Function, "get", Route = "test")] HttpRequest req, ILogger log)
    {
        return Task.FromResult<IActionResult>(new OkResult());
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
