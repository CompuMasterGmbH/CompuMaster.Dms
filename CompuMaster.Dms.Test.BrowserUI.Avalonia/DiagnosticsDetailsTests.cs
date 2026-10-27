using System.Net;
using System.Net.Http;
using NUnit.Framework;
#if DMS_WINUI
using CompuMaster.Dms.BrowserUI.WinUI;
namespace CompuMaster.Dms.Test.BrowserUI.WinUI;
#else
using CompuMaster.Dms.BrowserUI.Avalonia;
namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;
#endif

[TestFixture]
public sealed class DiagnosticsDetailsTests
{
    [Test]
    public void Details_retain_the_exception_chain_operation_and_http_status()
    {
        var cause = new HttpRequestException("PROPFIND failed: backend unavailable", null, HttpStatusCode.ServiceUnavailable);
        var error = new InvalidOperationException("Directory listing failed", cause);
        var text = BrowserErrorDetails.Format(error, DmsBrowserFailurePhase.ChildListing, "ExpandFolder");
        Assert.That(text, Does.Contain("ExpandFolder").And.Contain("ChildListing").And.Contain("InvalidOperationException"));
        Assert.That(text, Does.Contain("Directory listing failed").And.Contain("HttpRequestException").And.Contain("HTTP: 503"));
    }
    [Test]
    public void Credentials_authorization_and_url_queries_are_redacted()
    {
        var text = BrowserErrorDetails.Redact("GET https://alice:urlsecret@example.invalid/dav?token=querysecret#fragmentsecret\nAuthorization: Bearer headersecret\npassword=plainsecret\n<token>xmlsecret</token>\nCookie: sessionid=cookiesecret");
        foreach (var secret in new[] { "alice", "urlsecret", "querysecret", "fragmentsecret", "headersecret", "plainsecret", "xmlsecret", "cookiesecret" })
            Assert.That(text, Does.Not.Contain(secret));
        Assert.That(text, Does.Contain("https://example.invalid/dav"));
    }
    [Test]
    public void Aggregate_causes_and_provider_http_status_remain_visible()
    {
        var error = new AggregateException(new IOException("Interrupted transfer"), new CompuMaster.Dms.Data.ResponseStatusCodeException(423, "Locked"));
        var text = BrowserErrorDetails.Format(error, DmsBrowserFailurePhase.Action, "Download");
        Assert.That(text, Does.Contain("Interrupted transfer").And.Contain("HTTP: 423").And.Contain("Locked"));
    }
}
