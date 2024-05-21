using Blink;
using Blink.Discord;

public class Program
{
    /// <summary>
    /// Runs the program.
    /// </summary>
    /// <param name="args">Arguments from the command line.</param>
    public static void Main(string[] args)
    {
        // Load the configuration.
        Configuration.ReadConfiguration();
        Configuration.ListenForConfigurationChanges();
        
        // Start the Discord bot.
        Logger.Debug("Starting Discord bot.");
        Bot.GetBot().StartAsync().Wait();
        Logger.Debug("Started Discord bot.");
        
        // Keep the application alive.
        while (true) Console.ReadLine();
    }
}