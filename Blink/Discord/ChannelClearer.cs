﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace Blink.Discord;

public class ChannelClearer
{
    /// <summary>
    /// Id of the channel to clear messages from.
    /// </summary>
    public readonly ulong ChannelId;

    /// <summary>
    /// Maximum age (in seconds) a message can be in order to not be deleted.
    /// </summary>
    public double MessageMaxAgeSeconds;

    /// <summary>
    /// If true, the logic will run but no messages will be deleted.
    /// </summary>
    public bool DryRun = false;
    
    /// <summary>
    /// Bot used to interact with Discord.
    /// </summary>
    private readonly Bot _bot;

    /// <summary>
    /// Next message that will be deleted.
    /// </summary>
    private IMessage? _oldestMessage;

    /// <summary>
    /// Whether the clearer is running or not.
    /// </summary>
    private bool _running;
    
    /// <summary>
    /// Creates a channel clearer.
    /// </summary>
    /// <param name="bot">Discord bot that controls the application.</param>
    /// <param name="channelId">Id of the channel to clear.</param>
    /// <param name="messageMaxAgeSeconds">Maximum age (in seconds) a message can be in order to not be deleted.</param>
    /// <param name="dryRun">If true, the logic will run but no messages will be deleted.</param>
    public ChannelClearer(Bot bot, ulong channelId, double messageMaxAgeSeconds, bool dryRun)
    {
        this._bot = bot;
        this.ChannelId = channelId;
        this.MessageMaxAgeSeconds = messageMaxAgeSeconds;
        this.DryRun = dryRun;
    }
    
    /// <summary>
    /// Returns the first message in a text channel.
    /// </summary>
    /// <param name="textChannel">Text channel to get the first message of.</param>
    /// <returns>First channel of the text channel, if it exists.</returns>
    private static async Task<IMessage?> GetFirstMessageAsync(IMessageChannel textChannel)
    {
        var latestMessage = (await textChannel.GetMessagesAsync(1).FlattenAsync()).FirstOrDefault();
        if (latestMessage == null) return null;
        while (true)
        {
            var nextMessages = (await textChannel.GetMessagesAsync(latestMessage.Id, Direction.Before, limit: 100).FlattenAsync()).ToArray();
            if (nextMessages.Length == 0) return latestMessage;
            latestMessage = nextMessages.Last();
        }
    }

    /// <summary>
    /// Checks if a message can be bulk deleted.
    /// </summary>
    /// <param name="message">Message to delete.</param>
    /// <returns>Whether the message can be bulk deleted or not.</returns>
    private bool CanBulkDelete(IMessage message)
    {
        var messageAge = DateTime.Now - message.Timestamp;
        return messageAge.TotalDays <= 14;
    }

    /// <summary>
    /// Checks if a message should be deleted.
    /// </summary>
    /// <param name="message">Message to delete.</param>
    /// <returns>Whether the message should be deleted or not.</returns>
    private bool ShouldDeleteMessage(IMessage message)
    {
        var messageAge = DateTime.Now - message.Timestamp;
        return messageAge.TotalSeconds >= MessageMaxAgeSeconds;
    }

    /// <summary>
    /// Performs a delete step.
    /// Might throw an exception at any point.
    /// </summary>
    private async Task PerformDeleteStepAsync()
    {
        // Get the channel.
        Logger.Info($"Checking for messages to delete in {this.ChannelId}");
        var channel = this._bot.Client.GetChannel(this.ChannelId);
        if (channel == null)
        {
            Logger.Warn($"Channel {this.ChannelId} was not found.");
            return;
        }
        if (channel is not IMessageChannel messageChannel)
        {
            Logger.Warn($"Channel {this.ChannelId} was but was not a message channel.");
            return;
        }
        
        // Get the oldest message if it unset.
        if (this._oldestMessage == null || await messageChannel.GetMessageAsync(this._oldestMessage.Id) == null)
        {
            Logger.Debug($"Getting latest message for {this.ChannelId}.");
            var firstMessage = await GetFirstMessageAsync(messageChannel);
            if (firstMessage == null)
            {
                Logger.Info($"Channel {this.ChannelId} has no messages.");
                this._oldestMessage = null;
                return;
            }
            Logger.Debug($"Found first message {firstMessage.Id} in {this.ChannelId}.");
            this._oldestMessage = firstMessage;
        }
        
        // Return if the oldest message is too new.
        var oldestMessage = this._oldestMessage;
        if (!this.ShouldDeleteMessage(oldestMessage))
        {
            Logger.Info($"Oldest message in {this.ChannelId} ({oldestMessage.Id}) is too recent. No messages deleted.");
            return;
        }
        
        // Get the messages to delete.
        Logger.Debug($"Getting messages to delete in {this.ChannelId}.");
        var messagesToDelete = new List<IMessage> { oldestMessage };
        while (true)
        {
            // Get the messages and break the loop if no to delete messages were found.
            // Ordering is required to keep the newest message found at the end of the list.
            var nextMessages = (await messageChannel.GetMessagesAsync(messagesToDelete.Last().Id, Direction.After, limit: 100).FlattenAsync())
                .Where(message => messagesToDelete.All(otherMessage => otherMessage.Id != message.Id))
                .Where(this.ShouldDeleteMessage)
                .OrderBy(message => message.Timestamp)
                .ToArray();
            if (nextMessages.Length == 0) break;
            
            // Add the messages.
            messagesToDelete.AddRange(nextMessages);
        }
        Logger.Debug($"Found {messagesToDelete.Count} messages to delete in {this.ChannelId}.");
        
        // Get the last message after the deletions.
        var nextRemainingMessage = (await messageChannel.GetMessagesAsync(messagesToDelete.Last().Id, Direction.After, limit: 5).FlattenAsync())
            .OrderBy(message => message.Timestamp)
            .FirstOrDefault(message => messagesToDelete.All(otherMessage => otherMessage.Id != message.Id));
        
        // Try to bulk delete the messages.
        if (!this.DryRun && messageChannel is ITextChannel textChannel)
        {
            // Split the messages to delete into groups.
            var bulkDeletableMessages = messagesToDelete.Where(CanBulkDelete).ToList();
            var messagesToBulkDelete = new List<List<IMessage>>();
            for (var i = 0; i < bulkDeletableMessages.Count; i++)
            {
                if (i % 50 == 0) messagesToBulkDelete.Add(new List<IMessage>());
                messagesToBulkDelete.Last().Add(bulkDeletableMessages[i]);
            }
            
            // Try to delete the messages.
            // Bulk deletes won't work on messages >14 days old. It may not work and require individual deletions.
            // The optimization to keep the oldest message is not used due to the complications of this sometimes
            // and sometimes not working. Ideally, if this fails, the individual deletes will take over and fail,
            // or will complete and set the next message.
            foreach (var messageGroup in messagesToBulkDelete)
            {
                try
                {
                    await textChannel.DeleteMessagesAsync(messageGroup);
                    Logger.Info($"Bulk deleted {messageGroup.Count} messages in {textChannel.Id}.");
                    foreach (var message in messageGroup)
                    {
                        messagesToDelete.Remove(message);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"Failed to bulk delete {messageGroup.Count} messages in {textChannel.Id}.\n{e}");
                }
            }
        }
        
        // Delete the messages.
        for (var i = 0; i < messagesToDelete.Count; i++)
        {
            // Delete the message.
            var message = messagesToDelete[i];
            if (!this.DryRun)
            {
                Logger.Debug($"Deleting message {message.Id} in {this.ChannelId}.");
                await messageChannel.DeleteMessageAsync(message);
                Logger.Info($"Deleted message {message.Id} in {this.ChannelId}.");
            }
            else
            {
                Logger.Info($"[DRY RUN] Deleted message {message.Id} in {this.ChannelId}.");
            }
            
            // Set the next message to delete in case an exception occurs.
            if (i + 1 < messagesToDelete.Count())
            {
                this._oldestMessage = messagesToDelete[i + 1];
            }
        }
        
        // Set the next message that will be deleted.
        if (nextRemainingMessage != null)
        {
            this._oldestMessage = nextRemainingMessage;
            Logger.Debug($"Next message to be deleted in channel {this.ChannelId} will be {nextRemainingMessage.Id}.");
        }
        else
        {
            this._oldestMessage = null;
            Logger.Debug($"End of channel {this.ChannelId} reached. No more messages to delete.");
        }
    }

    /// <summary>
    /// Starts the channel clearer.
    /// </summary>
    public void Start()
    {
        if (this._running) return;
        this._running = true;
        Task.Run(async () =>
        {
            while (this._running)
            {
                try
                {
                    await this.PerformDeleteStepAsync();
                }
                catch (Exception e)
                {
                    Logger.Error($"Exception occured while clearing {this.ChannelId}:\n{e}");
                }
                await Task.Delay(Configuration.GetConfiguration().DeleteActionDelaySeconds * 1000);
            }
        });
    }

    /// <summary>
    /// Stops the channel clearer.
    /// </summary>
    public void Stop()
    {
        this._running = false;
    }
}