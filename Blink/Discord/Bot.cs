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
    private async Task ClientReadyHandler()
    {
        // TODO
    }
}