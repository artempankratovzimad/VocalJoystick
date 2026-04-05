using System;
using System.Collections.Generic;
using System.Linq;

namespace VocalJoystick.Core.Models;

public sealed class ProfileConfiguration
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public UserProfileMetadata Metadata { get; set; } = new();
    public Dictionary<VocalAction, ActionConfiguration> ActionConfigurations { get; set; } =
        Enum.GetValues<VocalAction>()
            .ToDictionary(action => action, action => new ActionConfiguration { Action = action });

    public IReadOnlyCollection<ActionConfiguration> ConfiguredActions => ActionConfigurations.Values;

    public static ProfileConfiguration CreateDefault(UserProfileMetadata metadata)
    {
        var config = new ProfileConfiguration
        {
            Metadata = metadata,
            ActionConfigurations = Enum.GetValues<VocalAction>()
                .ToDictionary(action => action, action => new ActionConfiguration { Action = action })
        };

        return config;
    }

    public ActionConfiguration GetConfiguration(VocalAction action)
    {
        if (ActionConfigurations.TryGetValue(action, out var configuration))
        {
            return configuration;
        }

        var defaultConfig = new ActionConfiguration { Action = action };
        ActionConfigurations[action] = defaultConfig;
        return defaultConfig;
    }
}
