using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace CompuMaster.Dms.BrowserUI.WinUI;

// Shell dialogs retain arbitrary default directories, multi-folder selection and HWND ownership.
// Selection itself never grants access outside the browser's LocalParentMustFolder policy.
internal static class NativePickers
{
    internal static Task<string[]> OpenAsync(Window owner, string title, string? folder, bool folders = false) =>
        Task.FromResult(Pick(owner, title, folder, folders, false, null));
    internal static Task<string?> SaveAsync(Window owner, string title, string? folder, string name) =>
        Task.FromResult(Pick(owner, title, folder, false, true, name).FirstOrDefault());
    private static string[] Pick(Window owner, string title, string? folder, bool folders, bool save, string? name)
    {
        var type = Type.GetTypeFromCLSID(new Guid(save ? "C0B4E2F3-BA21-4773-8DBA-335EC946EB8B" : "DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"))!;
        var instance = Activator.CreateInstance(type)!;
        var dialog = (IFileDialog)instance;
        try
        {
            dialog.GetOptions(out var flags);
            flags = (flags | 0x40u | 0x8u | 0x800u) & ~0x2u; // filesystem, unchanged cwd, existing parent; conflicts are confirmed separately.
            if (!save) flags |= 0x200u;
            if (folders) flags |= 0x20u;
            dialog.SetOptions(flags); dialog.SetTitle(title);
            if (!string.IsNullOrEmpty(name)) dialog.SetFileName(name);
            if (!string.IsNullOrEmpty(folder))
            {
                var iid = typeof(IShellItem).GUID;
                SHCreateItemFromParsingName(folder, 0, ref iid, out var item);
                try { dialog.SetFolder(item); } finally { Marshal.FinalReleaseComObject(item); }
            }
            var result = dialog.Show(WinRT.Interop.WindowNative.GetWindowHandle(owner));
            if (result == unchecked((int)0x800704c7)) return Array.Empty<string>();
            Marshal.ThrowExceptionForHR(result);
            if (save)
            {
                dialog.GetResult(out var item);
                try { item.GetDisplayName(0x80058000, out var path); return new[] { path }; }
                finally { Marshal.FinalReleaseComObject(item); }
            }
            ((IFileOpenDialog)instance).GetResults(out var items);
            try
            {
                items.GetCount(out var count); var paths = new List<string>();
                for (uint index = 0; index < count; index++)
                {
                    items.GetItemAt(index, out var item);
                    try { item.GetDisplayName(0x80058000, out var path); paths.Add(path); }
                    finally { Marshal.FinalReleaseComObject(item); }
                }
                return paths.ToArray();
            }
            finally { Marshal.FinalReleaseComObject(items); }
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, nint context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);
    [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig] int Show(nint owner);
        void SetFileTypes(uint count, nint types); void SetFileTypeIndex(uint index); void GetFileTypeIndex(out uint index);
        void Advise(nint events, out uint cookie); void Unadvise(uint cookie); void SetOptions(uint options); void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem folder); void SetFolder(IShellItem folder); void GetFolder(out IShellItem folder); void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name); void GetFileName(out nint name); void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label); void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label); void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, uint location); void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension); void Close(int result);
        void SetClientGuid(ref Guid guid); void ClearClientData(); void SetFilter(nint filter);
    }
    [ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(nint owner);
        void SetFileTypes(uint count, nint types); void SetFileTypeIndex(uint index); void GetFileTypeIndex(out uint index);
        void Advise(nint events, out uint cookie); void Unadvise(uint cookie); void SetOptions(uint options); void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem folder); void SetFolder(IShellItem folder); void GetFolder(out IShellItem folder); void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name); void GetFileName(out nint name); void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label); void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label); void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, uint location); void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension); void Close(int result);
        void SetClientGuid(ref Guid guid); void ClearClientData(); void SetFilter(nint filter); void GetResults(out IShellItemArray items); void GetSelectedItems(out IShellItemArray items);
    }
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint context, ref Guid handler, ref Guid iid, out nint value); void GetParent(out IShellItem parent);
        void GetDisplayName(uint kind, [MarshalAs(UnmanagedType.LPWStr)] out string name); void GetAttributes(uint mask, out uint attributes); void Compare(IShellItem other, uint hint, out int order);
    }
    [ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        void BindToHandler(nint context, ref Guid handler, ref Guid iid, out nint value); void GetPropertyStore(int flags, ref Guid iid, out nint value);
        void GetPropertyDescriptionList(nint key, ref Guid iid, out nint value); void GetAttributes(int flags, uint mask, out uint attributes);
        void GetCount(out uint count); void GetItemAt(uint index, out IShellItem item); void EnumItems(out nint items);
    }
}
