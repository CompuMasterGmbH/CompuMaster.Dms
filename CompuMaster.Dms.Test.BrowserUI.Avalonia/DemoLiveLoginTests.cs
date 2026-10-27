using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using CompuMaster.Dms.TestDemo.Avalonia;
using global::Avalonia.Headless.NUnit;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture, Category("RemoteDms"), Explicit("Uses real demo credentials and a remote DMS; run only in the authorized serialized server scope.")]
public sealed class DemoLiveLoginTests
{
    [AvaloniaTest]
    public async Task Cached_credentials_authorize_and_initialize_the_actual_browser_read_only()
    {
        Assert.That(Environment.GetEnvironmentVariable("DMS_AVALONIA_LIVE_LOGIN"), Is.EqualTo("1"), "Explicit live-login mode is required.");
        var validTarget = Enum.TryParse<DemoTarget>(Environment.GetEnvironmentVariable("DMS_AVALONIA_LIVE_TARGET"), out var target)
            && target is not DemoTarget.Offline && Enum.IsDefined(target);
        Assert.That(validTarget, Is.True, "Select one supported demo target.");
        var source = new DemoInputBuffer(target);
        var definition = new DemoDefinition(target);
        Assert.That(!string.IsNullOrWhiteSpace(source.Read("username")) && !string.IsNullOrEmpty(source.Read("password")),
            Is.True, "Required demo credentials are not available in the cache/environment.");
        Assert.That(!string.IsNullOrWhiteSpace(source.Read(definition.RequiresCustomer ? "customer no." : "server url")),
            Is.True, "The demo server/customer setting is not available.");
        var directory = Path.Combine(Path.GetTempPath(), "unittests_avalonia_live_login_" + Guid.NewGuid().ToString("N"));
        Assert.That(Directory.Exists(directory), Is.False);
        Directory.CreateDirectory(directory);
        BaseDmsProvider? authorized = null;
        var browserOpened = false;
        string? failureType = null;
        try
        {
            // Read original settings without copying or rewriting the credential cache on form close.
            var buffer = new DemoInputBuffer(target, directory, name =>
            {
                var field = source.Fields.FirstOrDefault(field => source.EnvironmentVariableName(field) == name);
                return field is null ? null : source.Read(field);
            });
            var login = new DemoLoginWindow(definition, async profile =>
            {
                try { return authorized = await DmsFactory.CreateAuthorizedDmsProviderInstanceAsync(profile); }
                catch (Exception ex) { failureType = ex.GetType().Name; throw; }
            }, async (_, provider, options) =>
            {
                var browser = new DmsBrowser(provider, options);
                // Prevent native input/clipboard/transfer actions: only initialize and list the configured root.
                try
                {
                    browser.Show();
                    await browser.Browser.InitializeAsync();
                    browserOpened = true;
                }
                catch (Exception ex) { failureType = ex.GetType().Name; throw; }
                finally { browser.Close(); }
            }, buffer, available =>
            {
                var expected = source.Read("expected teamwork tenant id");
                var chosen = string.IsNullOrEmpty(expected) ? (authorized as IDmsInstanceProvider)?.CurrentDmsInstance?.ID : expected;
                if (!available.Any(instance => instance.ID == chosen))
                    throw new InvalidOperationException("An explicit configured instance is required for unattended live login.");
                return Task.FromResult(chosen);
            });
            try
            {
                await login.ConnectAsync();
                Assert.That(browserOpened, Is.True, "Live authorization/browser initialization failed. Failure type: " + (failureType ?? "selection or input validation") + ". Provider diagnostics and credentials are intentionally omitted.");
                Assert.That(login.Error.Text == "", Is.True, "The demo reported a sign-in failure.");
            }
            finally { login.Close(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
