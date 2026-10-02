Option Explicit On
Option Strict On

Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

''' <summary>
''' Defines how a DMS instance is selected before a browser opens and how a later change can be requested.
''' </summary>
Public NotInheritable Class RemoteInstanceSelector

    ''' <summary>
    ''' Identifies the instance selection behavior at startup.
    ''' </summary>
    Public Enum StartupInstance As Byte
        ''' <summary>Uses the provider's default instance.</summary>
        DefaultInstance = 0
        ''' <summary>Uses a specified instance identifier.</summary>
        SpecifiedInstance = 1
        ''' <summary>Requests a selection when multiple instances are available.</summary>
        ''' <remarks>When only one instance is available, it is selected automatically.</remarks>
        SelectionDialog = 2
    End Enum

    ''' <summary>
    ''' Requests an instance identifier from the user.
    ''' </summary>
    ''' <param name="owner">The window that owns the selection dialog, or <see langword="Nothing"/>.</param>
    ''' <param name="instances">The available instances, including the currently selected instance.</param>
    ''' <returns>The selected instance identifier, or <see langword="Nothing"/> when the user cancels.</returns>
    Public Delegate Function SelectionDialogMethod(owner As IWin32Window, instances As IReadOnlyList(Of DmsInstanceInfo)) As String

    ''' <summary>Creates a selector that uses the default instance.</summary>
    Public Sub New()
        Me.New(StartupInstance.DefaultInstance, Nothing)
    End Sub

    ''' <summary>Creates a selector for the specified startup behavior.</summary>
    ''' <param name="startupInstanceMode">The startup behavior, except <see cref="StartupInstance.SpecifiedInstance"/>.</param>
    ''' <exception cref="NotSupportedException">A specified instance requires the overload accepting an identifier.</exception>
    Public Sub New(startupInstanceMode As StartupInstance)
        Me.New(startupInstanceMode, Nothing)
    End Sub

    ''' <summary>Creates a selector for the specified startup behavior and optional later selection.</summary>
    ''' <param name="startupInstanceMode">The startup behavior, except <see cref="StartupInstance.SpecifiedInstance"/>.</param>
    ''' <param name="selectionDialog">The selection method required for dialog startup and optional for later switching.</param>
    ''' <exception cref="ArgumentNullException">Dialog startup was requested without a selection method.</exception>
    ''' <exception cref="NotSupportedException">A specified instance requires the overload accepting an identifier.</exception>
    Public Sub New(startupInstanceMode As StartupInstance, selectionDialog As SelectionDialogMethod)
        Select Case startupInstanceMode
            Case StartupInstance.DefaultInstance
            Case StartupInstance.SpecifiedInstance
                Throw New NotSupportedException("Use the constructor accepting an instance identifier.")
            Case StartupInstance.SelectionDialog
                If selectionDialog Is Nothing Then Throw New ArgumentNullException(NameOf(selectionDialog))
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(startupInstanceMode))
        End Select
        Me.SelectedStartupInstance = startupInstanceMode
        Me.SelectionDialog = selectionDialog
    End Sub

    ''' <summary>Creates a selector that uses a specified instance.</summary>
    ''' <param name="instanceID">The identifier of the instance to open.</param>
    Public Sub New(instanceID As String)
        Me.New(instanceID, Nothing)
    End Sub

    ''' <summary>Creates a selector that uses a specified instance and optionally permits later selection.</summary>
    ''' <param name="instanceID">The identifier of the instance to open.</param>
    ''' <param name="selectionDialog">The optional selection method for later switching.</param>
    ''' <exception cref="ArgumentNullException">The instance identifier is <see langword="Nothing"/>.</exception>
    ''' <exception cref="ArgumentException">The instance identifier is empty or whitespace.</exception>
    Public Sub New(instanceID As String, selectionDialog As SelectionDialogMethod)
        If instanceID Is Nothing Then Throw New ArgumentNullException(NameOf(instanceID))
        If String.IsNullOrWhiteSpace(instanceID) Then Throw New ArgumentException("An instance identifier is required.", NameOf(instanceID))
        Me.InstanceID = instanceID
        Me.SelectedStartupInstance = StartupInstance.SpecifiedInstance
        Me.SelectionDialog = selectionDialog
    End Sub

    ''' <summary>Gets the startup behavior.</summary>
    Public ReadOnly Property SelectedStartupInstance As StartupInstance

    ''' <summary>Gets the specified startup instance identifier, if any.</summary>
    Public ReadOnly Property InstanceID As String

    ''' <summary>Gets the method used for startup selection or an optional later change.</summary>
    Public ReadOnly Property SelectionDialog As SelectionDialogMethod

    ''' <summary>Creates an authorized provider and selects its startup instance before a browser is created.</summary>
    ''' <param name="profile">The login profile used to authorize the provider.</param>
    ''' <param name="owner">The window that owns an optional selection dialog.</param>
    ''' <returns>The selected provider, or <see langword="Nothing"/> when the user cancels.</returns>
    ''' <exception cref="NotSupportedException">A specified instance was requested for a provider without instance selection.</exception>
    Public Function CreateSelectedProvider(profile As IDmsLoginProfile, owner As IWin32Window) As BaseDmsProvider
        If profile Is Nothing Then Throw New ArgumentNullException(NameOf(profile))
        Dim provider As BaseDmsProvider = DmsFactory.CreateAuthorizedDmsProviderInstance(profile)
        If Not Me.SelectProviderInstance(provider, owner) Then Return Nothing
        Return provider
    End Function

    Friend Function SelectProviderInstance(provider As BaseDmsProvider, owner As IWin32Window) As Boolean
        If provider Is Nothing Then Throw New ArgumentNullException(NameOf(provider))
        If Me.SelectedStartupInstance = StartupInstance.DefaultInstance Then Return True

        Dim instanceProvider As IDmsInstanceProvider = TryCast(provider, IDmsInstanceProvider)
        If instanceProvider Is Nothing Then
            If Me.SelectedStartupInstance = StartupInstance.SelectionDialog Then Return True
            Throw New NotSupportedException("The provider does not support selecting a DMS instance.")
        End If

        If Me.SelectedStartupInstance = StartupInstance.SpecifiedInstance Then
            instanceProvider.SelectDmsInstance(Me.InstanceID)
            Return True
        End If

        Dim instances As IReadOnlyList(Of DmsInstanceInfo) = instanceProvider.ListAvailableDmsInstances()
        If instances.Count = 0 Then Throw New InvalidOperationException(UiStrings.GetText("NoDmsInstances"))

        Dim instanceID As String
        If instances.Count = 1 Then
            instanceID = instances(0).ID
        Else
            instanceID = Me.SelectionDialog(owner, instances)
            If instanceID Is Nothing Then Return False
            If String.IsNullOrWhiteSpace(instanceID) OrElse
               Not instances.Any(Function(instance) String.Equals(instance.ID, instanceID, StringComparison.Ordinal)) Then
                Throw New ArgumentOutOfRangeException(NameOf(instanceID), "The selected DMS instance is not available.")
            End If
        End If

        Dim currentInstance As DmsInstanceInfo = instanceProvider.CurrentDmsInstance
        If currentInstance Is Nothing OrElse Not String.Equals(currentInstance.ID, instanceID, StringComparison.Ordinal) Then
            instanceProvider.SelectDmsInstance(instanceID)
        End If
        Return True
    End Function

    ''' <summary>Shows the built-in instance selection dialog.</summary>
    ''' <param name="owner">The window that owns the dialog, or <see langword="Nothing"/>.</param>
    ''' <param name="instances">The available instances.</param>
    ''' <returns>The selected instance identifier, or <see langword="Nothing"/> when the user cancels.</returns>
    Public Shared Function ShowDefaultSelectionDialog(owner As IWin32Window, instances As IReadOnlyList(Of DmsInstanceInfo)) As String
        Dim ownerForm As Form = TryCast(owner, Form)
        Return ShowDefaultSelectionDialog(owner, instances, ownerForm?.Icon)
    End Function

    ''' <summary>Shows the built-in instance selection dialog with the specified window icon.</summary>
    ''' <param name="owner">The window that owns the dialog, or <see langword="Nothing"/>.</param>
    ''' <param name="instances">The available instances.</param>
    ''' <param name="formIcon">The icon shown on the selection dialog, or <see langword="Nothing"/> for the browser icon.</param>
    ''' <returns>The selected instance identifier, or <see langword="Nothing"/> when the user cancels.</returns>
    Public Shared Function ShowDefaultSelectionDialog(owner As IWin32Window, instances As IReadOnlyList(Of DmsInstanceInfo), formIcon As Drawing.Icon) As String
        If instances Is Nothing Then Throw New ArgumentNullException(NameOf(instances))
        Using picker As New DmsInstanceSelectionDialog(instances, formIcon)
            Dim result As DialogResult = If(owner Is Nothing, picker.ShowDialog(), picker.ShowDialog(owner))
            If result <> DialogResult.OK Then Return Nothing
            Return picker.SelectedInstance?.ID
        End Using
    End Function

End Class
