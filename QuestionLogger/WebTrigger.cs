using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Twilio.Security;

namespace QuestionLogger;

public static class WebTrigger
{
    [FunctionName("SmsResponse")]
    public static async Task<IActionResult> ReportResponseSms([HttpTrigger(AuthorizationLevel.Function, "post", Route = "sms-in")] HttpRequest req, ILogger log)
    {
        var form = await req.ReadFormAsync();
        if (!IsTwilioAuthorized(req, form))
        {
            return new UnauthorizedResult();
        }
        if (!string.Equals(FunctionHelper.GetEnvironmentVariable("ToPhoneNumber"), form["From"]))
        {
            return new UnauthorizedResult();
        }

        var answerToQuestion = form["Body"];
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

    private static bool IsTwilioAuthorized(HttpRequest req, IFormCollection form)
    {
        //see: https://www.twilio.com/docs/usage/tutorials/how-to-secure-your-csharp-aspnet-core-app-by-validating-incoming-twilio-requests#use-filter-attribute-to-validate-twilio-requests
        var authToken = FunctionHelper.GetEnvironmentVariable("TwilioAuth");
        var host = FunctionHelper.GetEnvironmentVariable("Host");
        var url  = $"{host}{req.Path}";
        var twilioSignature  = req.Headers["X-Twilio-Signature"];
        var parameters = form.ToDictionary(p => p.Key, p => p.Value.ToString());
        var validator = new RequestValidator(authToken);
        return validator.Validate(url, parameters, twilioSignature);
    }
}
