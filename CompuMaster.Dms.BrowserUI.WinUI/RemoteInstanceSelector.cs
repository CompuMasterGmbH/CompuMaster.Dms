using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using Microsoft.UI.Xaml;

namespace CompuMaster.Dms.BrowserUI.WinUI;

/// <summary>Defines startup instance selection and optional later switching.</summary>
public sealed class RemoteInstanceSelector
{
    /// <summary>Identifies startup instance selection behavior.</summary>
    public enum StartupInstance : byte
    {
        /// <summary>Preserves the provider's default or explicitly selected instance.</summary>
        DefaultInstance,
        /// <summary>Selects a specified instance identifier.</summary>
        SpecifiedInstance,
        /// <summary>Asks the user when more than one instance is available.</summary>
        SelectionDialog
    }
    /// <summary>Asynchronously selects an instance identifier, or null to cancel.</summary>
    /// <param name="owner">The owning WinUI window.</param>
    /// <param name="instances">The available instances.</param>
    /// <returns>The selected identifier, or null.</returns>
    public delegate Task<string?> SelectionDialogMethod(Window owner, IReadOnlyList<DmsInstanceInfo> instances);

    /// <summary>Creates a selector preserving the current provider instance.</summary>
    public RemoteInstanceSelector() { }
    /// <summary>Creates a selector for startup selection and optional later switching.</summary>
    /// <param name="mode">The startup mode; specified instances require the identifier overload.</param>
    /// <param name="selectionDialog">The optional selection method; defaults to the built-in dialog for dialog startup.</param>
    public RemoteInstanceSelector(StartupInstance mode, SelectionDialogMethod? selectionDialog = null)
    {
        if (mode is not (StartupInstance.DefaultInstance or StartupInstance.SelectionDialog))
            throw new ArgumentOutOfRangeException(nameof(mode));
        SelectedStartupInstance = mode;
        SelectionDialog = selectionDialog ?? (mode == StartupInstance.SelectionDialog ? ShowDefaultSelectionDialogAsync : null);
    }
    /// <summary>Creates a selector for an explicit instance and optional later switching.</summary>
    /// <param name="instanceID">The identifier of the instance to open.</param>
    /// <param name="selectionDialog">The optional method for later switching.</param>
    public RemoteInstanceSelector(string instanceID, SelectionDialogMethod? selectionDialog = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceID);
        InstanceID = instanceID;
        SelectedStartupInstance = StartupInstance.SpecifiedInstance;
        SelectionDialog = selectionDialog;
    }
    /// <summary>Gets the startup behavior.</summary>
    public StartupInstance SelectedStartupInstance { get; }
    /// <summary>Gets the explicitly requested identifier.</summary>
    public string? InstanceID { get; }
    /// <summary>Gets the optional method for startup selection and later switching.</summary>
    public SelectionDialogMethod? SelectionDialog { get; }

    internal async Task<bool> SelectAsync(BaseDmsProvider provider, Window owner)
    {
        if (SelectedStartupInstance == StartupInstance.DefaultInstance) return true;
        if (provider is not IDmsInstanceProvider instances)
        {
            if (SelectedStartupInstance == StartupInstance.SelectionDialog) return true;
            throw new NotSupportedException("The provider does not support selecting a DMS instance.");
        }
        if (SelectedStartupInstance == StartupInstance.SpecifiedInstance)
        {
            await Task.Run(() => instances.SelectDmsInstance(InstanceID));
            return true;
        }
        var available = await Task.Run(instances.ListAvailableDmsInstances);
        if (available.Count == 0) throw new InvalidOperationException(UiText.Get("NoDmsInstances"));
        var id = available.Count == 1 ? available[0].ID : await SelectionDialog!(owner, available);
        if (id is null) return false;
        Validate(available, id);
        if (instances.CurrentDmsInstance?.ID != id) await Task.Run(() => instances.SelectDmsInstance(id));
        return true;
    }
    internal static void Validate(IReadOnlyList<DmsInstanceInfo> instances, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !instances.Any(i => i.ID == id))
            throw new ArgumentOutOfRangeException(nameof(id), "The selected DMS instance is not available.");
    }
    /// <summary>Shows the built-in instance selection dialog.</summary>
    /// <param name="owner">The owning window.</param>
    /// <param name="instances">The available instances.</param>
    /// <returns>The confirmed instance identifier, or null on cancellation.</returns>
    public static async Task<string?> ShowDefaultSelectionDialogAsync(Window owner, IReadOnlyList<DmsInstanceInfo> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);
        var ordered = instances.OrderByDescending(i => i.IsSelected)
            .ThenBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.ID, StringComparer.Ordinal).ToArray();
        return (await Dialogs.ChooseAsync(owner, UiText.Get("DmsInstanceSelectionTitle"),
            ordered.Select(i => new DialogChoice(i.ID, i.DisplayName)).ToArray(),
            ordered.FirstOrDefault(i => i.IsSelected)?.ID, UiText.Get("DmsInstanceSelectionPrompt")))?.ID;
    }
}
