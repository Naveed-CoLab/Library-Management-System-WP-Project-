using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Text.Json;

namespace System.Notifications;

public sealed record SnackbarMessage(string Kind, string Message);

public static class SnackbarTempDataExtensions
{
    private const string SnackbarKey = "SnackbarMessages";

    public static void NotifySuccess(this ITempDataDictionary tempData, string message) =>
        tempData.AddSnackbar("success", message);

    public static void NotifyWarning(this ITempDataDictionary tempData, string message) =>
        tempData.AddSnackbar("warning", message);

    public static void NotifyError(this ITempDataDictionary tempData, string message) =>
        tempData.AddSnackbar("danger", message);

    public static void NotifyInfo(this ITempDataDictionary tempData, string message) =>
        tempData.AddSnackbar("info", message);

    private static void AddSnackbar(this ITempDataDictionary tempData, string kind, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var messages = new List<SnackbarMessage>();
        if (tempData.TryGetValue(SnackbarKey, out var existing) && existing is string json && !string.IsNullOrWhiteSpace(json))
        {
            messages = JsonSerializer.Deserialize<List<SnackbarMessage>>(json) ?? new List<SnackbarMessage>();
        }

        messages.Add(new SnackbarMessage(kind, message.Trim()));
        tempData[SnackbarKey] = JsonSerializer.Serialize(messages);
    }
}
