using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace QuestionLogger.Util;

public static class SheetsHelper
{
    public static async Task<string?> GetQuestion()
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

    public static async Task LogQuestion(string question)
    {
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
}