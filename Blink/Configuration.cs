﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

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
    /// Semaphore for reloading the configuration.
    /// </summary>
    private static SemaphoreSlim _configurationReloadSemaphore = new SemaphoreSlim(1);
    
    /// <summary>
    /// Contents of the last configuration to avoid duplicate reloads.
    /// </summary>
    private static string? _lastConfiguration; 
    
    /// <summary>
    /// Event for configuration being loaded or reloaded.
    /// </summary>
    public static event Action<Configuration>? ConfigurationLoaded;
    
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
    /// Returns the path the configuration.
    /// </summary>
    /// <returns>Path of the configuration.</returns>
    private static string GetConfigurationPath()
    {
        var path = Environment.GetEnvironmentVariable("CONFIGURATION_FILE_LOCATION");
        return path ?? Path.Combine(Directory.GetCurrentDirectory(), "configuration.json");
    }
    
    /// <summary>
    /// Reads the configuration from the file system.
    /// </summary>
    public static void ReadConfiguration()
    {
        // Store the original Discord API key in case it changed.
        // If it is changed, warn that the application needs to be restarted.
        _configurationReloadSemaphore.Wait();
        string? originalDiscordToken = null;
        if (_configuration != null)
        {
            originalDiscordToken = _configuration.DiscordApiKey;
        }
        
        // Throw an exception if the configuration does not exist.
        var configurationPath = GetConfigurationPath();
        if (!File.Exists(configurationPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configurationPath}");
        }
        
        // Load the configuration.
        // Done with a retry in case the file in use or incomplete while writing.
        for (var i = 1; i <= 100; i++)
        {
            try
            {
                var configurationText = File.ReadAllText(configurationPath);
                if (configurationText != _lastConfiguration)
                {
                    _configuration = JsonSerializer.Deserialize(configurationText, ConfigurationJsonContext.Default.Configuration)!;
                    ConfigurationLoaded?.Invoke(_configuration);
                    _lastConfiguration = configurationText;
                    if (originalDiscordToken != null)
                    {
                        Logger.Info("Configuration reloaded.");
                    }
                }
                break;
            }
            catch (Exception e)
            {
                if (i == 100)
                {
                    Logger.Error($"Failed to be able to read {configurationPath}. The configuration was not reloaded.\n{e}");
                }
                else
                {
                    Task.Delay(100).Wait();
                }
            }
        }
        
        // Warn if the configuration changed.
        if (originalDiscordToken != null && originalDiscordToken != _configuration!.DiscordApiKey)
        {
            Logger.Warn("Discord API key refreshing is not supported. A restart of the application is required.");
        }
        _configurationReloadSemaphore.Release();
    }

    /// <summary>
    /// Connects listening to changes to the configuration file.
    /// </summary>
    public static void ListenForConfigurationChanges()
    {
        // Set up file change notifications.
        var configurationPath = GetConfigurationPath();
        var fileSystemWatcher = new FileSystemWatcher(Directory.GetParent(configurationPath)!.FullName);
        fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite;
        fileSystemWatcher.Changed += (_, _) => ReadConfiguration();
        fileSystemWatcher.EnableRaisingEvents = true;
        
        // Occasionally reload the file in a loop.
        // File change notifications don't seem to work in Docker with volumes.
        Task.Run(() =>
        {
            while (true)
            {
                Task.Delay(10000).Wait();
                ReadConfiguration();
            }
        });
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