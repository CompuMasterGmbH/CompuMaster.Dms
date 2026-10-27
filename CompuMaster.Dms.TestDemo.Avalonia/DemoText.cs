using System.Globalization;
using System.Resources;

#if DMS_WINUI
namespace CompuMaster.Dms.TestDemo.WinUI;
#else
namespace CompuMaster.Dms.TestDemo.Avalonia;
#endif

internal static class DemoText
{
    private static readonly ResourceManager Resources = new(typeof(DemoText).Namespace + ".DemoStrings", typeof(DemoText).Assembly);
    internal static string Get(string key) => (Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key).Replace("&", "");
    internal static string Extra(string en, string de) => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? de : en;
}
