# FivestaRSS: RSS for App Store and Google Play reviews

FivestaRSS is a tiny self-hosted service that monitors your apps' reviews on both stores, converting them into an RSS 2.0 feed (one per app). You can then easily plug the feed into your Slack, Discord or any other place that supports RSS.

## Features

- No database, uses feed files as the only persistent storage for previously fetched reviews
- Any server/API errors are published directly into the feed, so you won't miss service failures
- Configurable interval checks for new reviews

## Installation

Before you begin, you'll need to get both App Store Connect and Google Play Console API keys, [see these instructions](docs/api-setup.md). Then proceed to the [deployment manual](docs/deployment.md).

Once it's up and running, [see this guide](docs/chats.md) to connect it to Discord or Slack servers.

⭐️⭐️⭐️⭐️⭐️
