using Discord;
using Discord.WebSocket;

namespace Blink.Discord;

public class Bot
{
    /// <summary>
    /// Static instance of the bot.
    /// </summary>
    private static Bot? _bot;
    
    /// <summary>
    /// Client used for Discord.
    /// </summary>
    public readonly DiscordSocketClient Client;

    /// <summary>
    /// Channel cleaners that are running.
    /// </summary>
    private readonly Dictionary<ulong, ChannelClearer> _channelClearers = new Dictionary<ulong, ChannelClearer>();
    
    /// <summary>
    /// Returns the static instance of the bot.
    /// </summary>
    /// <returns>The static instance of the bot.</returns>
    public static Bot GetBot()
    {
        _bot ??= new Bot();
        return _bot;
    }
    
    /// <summary>
    /// Creates a Bot.
    /// </summary>
    public Bot()
    {
        this.Client = new DiscordSocketClient(new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.Guilds,
        });
    }
    
    /// <summary>
    /// Starts the Discord bot.
    /// </summary>
    public async Task StartAsync()
    {
        // Initialize the bot.
        this.Client.Log += (message) =>
        {
            Logger.Debug(message.ToString());
            return Task.CompletedTask;
        };
        this.Client.Ready += this.ClientReadyHandler;
        
        // Start the bot.
        await this.Client.LoginAsync(TokenType.Bot, Configuration.GetConfiguration().DiscordApiKey);
        await this.Client.StartAsync();
    }

    /// <summary>
    /// Handles the bot being ready.
    /// </summary>
    private Task ClientReadyHandler()
    {
        Configuration.ConfigrationLoaded += _ => UpdateChannelClearers();
        this.UpdateChannelClearers();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates or updates the channel clearers.
    /// </summary>
    private void UpdateChannelClearers()
    {
        // Update the channel clearers.
        var configurationChannels = Configuration.GetConfiguration().Channels;
        foreach (var configurationChannel in configurationChannels.Where(configuration => this._channelClearers.ContainsKey(configuration.ChannelId)))
        {
            Logger.Debug($"Updating channel configuration for {configurationChannel.ChannelId}.");
            var channelClearer = this._channelClearers[configurationChannel.ChannelId];
            channelClearer.MessageMaxAgeSeconds = configurationChannel.MessageMaxAgeSeconds;
            channelClearer.DryRun = configurationChannel.DryRun ?? false;
        }
        
        // Create the new channel clearers.
        foreach (var configurationChannel in configurationChannels.Where(configuration => !this._channelClearers.ContainsKey(configuration.ChannelId)))
        {
            Logger.Debug($"Starting channel configuration for {configurationChannel.ChannelId}.");
            var channelClearer = new ChannelClearer(this, configurationChannel.ChannelId, configurationChannel.MessageMaxAgeSeconds, configurationChannel.DryRun ?? false);
            this._channelClearers[configurationChannel.ChannelId] = channelClearer;
            channelClearer.Start();
        }
        
        // Remove the old channel clearers.
        foreach (var (channelId, channelClearer) in this._channelClearers.Where(clearer => configurationChannels.All(configuration => configuration.ChannelId != clearer.Key)).ToArray())
        {
            Logger.Debug($"Stopping channel configuration for {channelId}.");
            channelClearer.Stop();
            this._channelClearers.Remove(channelId);
        }
    }
}