using CommunityToolkit.Mvvm.Messaging.Messages;
using ManagerIV.Core;

namespace ManagerIV.Messages;

/// <summary>
/// Sent when the active profile changes, so the Library can update the checkboxes.
/// </summary>
public class ActiveProfileChangedMessage : ValueChangedMessage<Profile?>
{
    public ActiveProfileChangedMessage(Profile? value) : base(value) { }
}

/// <summary>
/// Sent when the Library load order changes or mods are enabled/disabled, 
/// so the MainViewModel can save the profile and update the Watchdog.
/// </summary>
public class LibraryStateChangedMessage : ValueChangedMessage<Profile>
{
    public LibraryStateChangedMessage(Profile profileToSave) : base(profileToSave) { }
}

/// <summary>
/// Sent when the Library wants to update the status text.
/// </summary>
public class StatusTextMessage : ValueChangedMessage<string>
{
    public StatusTextMessage(string value) : base(value) { }
}

/// <summary>
/// Sent when the Library needs to toggle the IsBusy state.
/// </summary>
public class IsBusyMessage : ValueChangedMessage<bool>
{
    public IsBusyMessage(bool value) : base(value) { }
}
