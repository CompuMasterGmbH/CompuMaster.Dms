using System.Globalization;
using System.Resources;

namespace CompuMaster.Dms.TestDemo.Avalonia;

internal static class DemoText
{
    private static readonly ResourceManager Resources = new("CompuMaster.Dms.TestDemo.Avalonia.DemoStrings", typeof(DemoText).Assembly);
    internal static string Get(string key) => (Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key).Replace("&", "");
    internal static string Extra(string en, string de) => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? de : en;
}
