# Blink
*Blink, and you miss it.*

Blink is a Discord bot for clearing older Discord messages in servers.
It is intended for servers that want to have a message history, but not
more than a few days. The bot is designed to be able to handle larger
channels with longer retention times, and was created after facing issues
with other bots when setting the retention time longer.

**Note:** While the bot does not have access to message contents (assuming
the message contents intents is not active, that is), Discord most likely
stores your deleted messages. The bot will not cover for anything against
Discord's ToS.

## Configuration
Configuration is currently handled with a `configuration.json` file. There
are no Discord commands to configure it. The configuration file can look like
the following:

```json
{
    "DiscordApiKey": "DISCORE_API_KEY",
    "DeleteActionDelaySeconds": 60,
    "Channels": [
        {
            "ChannelId": 12345678901234567890,
            "MessageMaxAgeSeconds": 259200,
            "DryRun": false
        },
        {
            "ChannelId": 12345678901234567890,
            "MessageMaxAgeSeconds": 259200,
            "DryRun": false
        }
    ]
}
```

- `DiscordApiKey` is the Discord API key of the bot. Changing this requires
  restarting the application.
- `DeleteActionDelaySeconds` is the delay (in seconds) between checking for
  messages to delete.
- `Channels` is the list of channels that are cleared. Included is:
  - `ChannelId`: Id of the channel in Discord.
  - `MessageMaxAgeSeconds`: Amount of seconds a message can be up before
    being deleted.
  - `DryRun` (optional): If true, the logic for delete messages will run
    and show in the logs, but won't actually delete messages.

Except for `DiscordApiKey`, all configuration entries can be changed
while the application is running.

## Hosted Bot
At the moment, no public bot is provided. This is due to the limitations
of the configuration and potential API rate limiting with 1 massive bot.
Please self-host or consider an alternative.

## Running (Docker)
Blink is intended to be run in Docker with the provided `docker-compose.yml`
file. `docker compose up -d --build` will build and start the application,
but a directory named `configuration` in the root project directory with
the `configuration.json` is required.

## License
Blink is available under the terms of the GNU Lesser General Public
License. See [LICENSE](LICENSE) for details.