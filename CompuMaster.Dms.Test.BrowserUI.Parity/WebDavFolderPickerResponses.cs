using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CompuMaster.Dms.Providers;

namespace CompuMaster.Dms.Test.BrowserUI;

internal sealed class WebDavFolderPickerResponses(string plusInHref) : HttpMessageHandler
{
    private const string Root = "/remote.php/dav/files/account/";
    private const string Parent = "RemoteDmsSimulation";
    internal const string Folder = "SuSa+BWA";
    internal const string SelectedPath = Parent + "/" + Folder;

    internal WebDavDmsProvider CreateProvider()
    {
        var provider = new WebDavDmsProvider { CustomWebApiUrl = "https://example.test" + Root };
        var field = typeof(WebDavDmsProvider).GetField("WebDavClient", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(provider, new WebDav.WebDavClient(new HttpClient(this, disposeHandler: false)));
        return provider;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parentHref = Root + Parent + "/";
        var plusHref = parentHref + "SuSa" + plusInHref + "BWA/";
        var spaceHref = parentHref + "SuSa%20BWA/";
        bool includeChildren = request.Headers.GetValues("Depth").Contains("1");
        string entries = request.RequestUri!.AbsolutePath.TrimEnd('/') switch
        {
            var path when path == Root.TrimEnd('/') => Response(Root, "", 1) +
                (includeChildren ? Response(parentHref, Parent, 2) : ""),
            var path when path == parentHref.TrimEnd('/') => Response(parentHref, Parent, 2) +
                (includeChildren ? Response(spaceHref, "SuSa BWA", 0) + Response(plusHref, Folder, 0) : ""),
            var path when Uri.UnescapeDataString(path) == Root + SelectedPath => Response(plusHref, Folder, 0),
            _ => throw new InvalidOperationException("Unexpected folder picker request: " + request.RequestUri)
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MultiStatus)
        {
            Content = new StringContent(
                "<d:multistatus xmlns:d='DAV:' xmlns:nc='http://nextcloud.org/ns'>" + entries + "</d:multistatus>",
                Encoding.UTF8, "application/xml")
        });
    }

    private static string Response(string href, string name, int childCount) =>
        $"<d:response><d:href>{href}</d:href><d:propstat><d:prop><d:displayname>{name}</d:displayname>" +
        $"<d:resourcetype><d:collection/></d:resourcetype><nc:contained-folder-count>{childCount}</nc:contained-folder-count>" +
        "</d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>";
}
