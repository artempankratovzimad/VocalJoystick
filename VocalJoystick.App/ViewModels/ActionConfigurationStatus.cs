using VocalJoystick.Core.Models;

namespace VocalJoystick.App.ViewModels;

public sealed record ActionConfigurationStatus(VocalAction Action, bool IsConfigured, string Status)
{
    public string DisplayName => Action.ToString();
    public string FriendlyStatus => Status;
}
