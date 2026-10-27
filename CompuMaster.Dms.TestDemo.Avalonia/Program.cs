namespace CompuMaster.Dms.TestDemo.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => DemoStartup.Run(DemoDefinition.FromArguments(args), args);
}
