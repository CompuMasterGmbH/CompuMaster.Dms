using System.Globalization;
using System.Resources;

namespace CompuMaster.Dms.BrowserUI.WinUI;

internal static class UiText
{
    internal static Microsoft.UI.Xaml.FlowDirection LayoutDirection =>
        CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? Microsoft.UI.Xaml.FlowDirection.RightToLeft :
        Microsoft.UI.Xaml.FlowDirection.LeftToRight;
    private static readonly ResourceManager Resources = new("CompuMaster.Dms.BrowserUI.WinUI.UiStrings", typeof(UiText).Assembly);
    internal static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    internal static string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);
    internal static string Extra(string en, string de) => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? de : en;
    internal static string FailureMessage(Exception exception)
    {
        if (exception is Data.DmsUserInputInvalidException or Data.DmsUserErrorMessageException)
            foreach (System.Collections.DictionaryEntry entry in Resources.GetResourceSet(CultureInfo.CurrentUICulture, true, true)!)
                if (entry.Value is string message && !message.Contains('{') && message == exception.Message) return message;
        return Get("BrowserOperationFailed");
    }
    internal static string ByteSize(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        var units = new[] { "Bytes", "KB", "MB", "GB", "TB", "PB" };
        var index = 0;
        while (index < 5 && value >= 1300d * Math.Pow(1000, index)) index++;
        return (value / Math.Pow(1024, index)).ToString("#,##0", CultureInfo.CurrentCulture) + " " + units[index];
    }
}
