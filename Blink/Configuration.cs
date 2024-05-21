using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blink;

public class ChannelConfigurationEntry
{
    /// <summary>
    /// Id of the channel to clear.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Age (in seconds) that a message can be before it is deleted.
    /// </summary>
    public double MessageMaxAgeSeconds { get; set; }
        
    /// <summary>
    /// If true, the logic for deleting messages will run, but no messages will be deleted.
    /// </summary>
    public bool? DryRun { get; set; }
}

[JsonSerializable(typeof(Configuration))]
[JsonSourceGenerationOptions(IncludeFields = true)]
internal partial class ConfigurationJsonContext : JsonSerializerContext
{
}

public class Configuration
{
    /// <summary>
    /// Static instance of a configuration.
    /// </summary>
    private static Configuration? _configuration;
    
    /// <summary>
    /// API key used to communicate with Discord.
    /// </summary>
    public string DiscordApiKey { get; set; } = "";

    /// <summary>
    /// Duration (in seconds) between attempting to delete messages.
    /// During each step, all the messages that can be deleted will be deleted, and then
    /// it will pause this amount of time to check if the oldest message is too old.
    /// </summary>
    public int DeleteActionDelaySeconds { get; set; } = 5 * 60;

    /// <summary>
    /// Channels that are cleared by the bot.
    /// </summary>
    public List<ChannelConfigurationEntry> Channels { get; set; } = new List<ChannelConfigurationEntry>();

    /// <summary>
    /// Reads the configuration from the file system.
    /// </summary>
    public static void ReadConfiguration()
    {
        // Throw an exception if the configuration does not exist.
        var configurationPath = "configuration.json"; // TODO: Check environment variable.
        if (!File.Exists(configurationPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configurationPath}");
        }
        
        // Load the configuration.
        _configuration = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(configurationPath), ConfigurationJsonContext.Default.Configuration)!;
    }

    /// <summary>
    /// Returns the currently loaded configuration.
    /// Loads the configuration if none exists.
    /// </summary>
    /// <returns>The currently loaded configuration.</returns>
    public static Configuration GetConfiguration()
    {
        if (_configuration == null)
        {
            ReadConfiguration();
        }
        return _configuration!;
    }
}