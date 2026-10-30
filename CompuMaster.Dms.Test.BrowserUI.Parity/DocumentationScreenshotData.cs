using System;
using CompuMaster.Dms.Data;
#if DMS_WINUI
using CompuMaster.Dms.TestDemo.WinUI;
#else
using CompuMaster.Dms.TestDemo.Avalonia;
#endif

namespace CompuMaster.Dms.Test.Screenshots;

// Process-local documentation fixtures: no credentials, server or payload transfer.
internal static class DocumentationScreenshotData
{
    internal const string Title = "DMS browser (offline example)";
    internal const string DetailsTitle = "Project brief.pdf — properties";
    internal const string Details = "Name: Project brief.pdf\nFolder: /Documents\nSize: 2.40 MiB\nModified: 2026-10-30 10:45\nOwner: Documentation team\nSharing: Review team (view and download)\n\nOffline documentation example.\nAll files and people shown here are synthetic.";
    internal static readonly string[] Names = { "Project brief.pdf", "Quarterly report.xlsx", "Release notes.txt", "Team notes.docx" };
    internal static DemoDmsProvider CreateProvider()
    {
        var provider = new DemoDmsProvider(false);
        provider.AddDirectory("/Documents"); provider.AddDirectory("/Documents/Projects");
        provider.AddDirectory("/Archive"); provider.AddDirectory("/Team files", DmsResourceItem.ItemTypes.Collection);
        var sizes = new long[] { 2516582, 786432, 4096, 18432 };
        for (var index = 0; index < Names.Length; index++)
        {
            var file = provider.AddFile("/Documents/" + Names[index], "Offline documentation fixture.", "unittests_readme_" + index);
            file.ContentLength = sizes[index];
            file.LastModificationOnLocalTime = new DateTime(2026, 10, 30, 10, 45 - 15 * index, 0);
            file.CreatedOnLocalTime = new DateTime(2026, 10, 1, 9, 0, 0);
            file.ExtendedInfosOwner = new DmsUser { ID = "unittests_documentation_team", DisplayName = "Documentation team" };
        }
        var shared = provider.Items.Find(item => item.Name == Names[0])!;
        shared.ExtendedInfosIsShared = true; shared.ExtendedInfosHasOutgoingSharings = true; shared.ExtendedInfosHasUserSharings = true;
        shared.ExtendedInfosUserSharings.Add(new DmsShareForUser(shared, new DmsUser { ID = "unittests_review_team", DisplayName = "Review team" }, true, true, false, false, false, false));
        return provider;
    }
}
