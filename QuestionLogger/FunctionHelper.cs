using System;
using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;

namespace QuestionLogger;

public static class FunctionHelper
{
    public static string GetEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) ??
               Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) ??
               string.Empty;
    }

    public static SheetsService GetSheetsService()
    {
        var text = Encoding.UTF8.GetString(Convert.FromBase64String(GetEnvironmentVariable("GoogleAuthBase64")));
        var credential = GoogleCredential.FromJson(text).CreateScoped(SheetsService.Scope.Spreadsheets);
        return new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Checkin App",
        });
    }

    /// <summary>
    /// DateTime result desired - the ConvertTimeFromUtc returns that type and an implicit cast would be wrong
    /// </summary>
    /// <returns></returns>
    public static DateTime GetCentralTimeNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.UtcNow.UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"));
    }

    public static string DateFormat => "M/d/yyyy hh:mm tt";
}
