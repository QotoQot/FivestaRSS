## Slack Integration

### Method 1: Slack RSS App (Recommended)

[See Slack's official documenation](https://slack.com/help/articles/218688467-Add-RSS-feeds-to-Slack)

### Method 2: Zapier Integration

For advanced automation:

1. **Create Zap:**
   - Trigger: RSS by Zapier > New Item in Feed
   - Feed URL: `https://yourdomain.com/feeds/your-app.xml`

2. **Configure Action:**
   - Action: Slack > Send Channel Message
   - Customize message format with review details (Title, Pub Date, Description)

## Discord Integration

### Method 1: Custom Discord Webhook

For simple notifications without bots:

1. **Create Zap:**
   - Trigger: RSS by Zapier > New Item in Feed
   - Feed URL: `https://yourdomain.com/feeds/your-app.xml`

2. **Configure Action:**
   - Action: Discord > Send Channel Message
   - Customize message format with review details (Title, Pub Date, Description)

### Method 2: Self-hosted Discord Bot for RSS

While there are many bots, I haven't found one that allows to configure RSS aside from the self-hosted [FeedCord](https://github.com/Qolors/FeedCord).

## Troubleshooting

**Formatting issues:**
- Test RSS feed in an [RSS viewer first](https://thibault.sh/tools/dev/rss-viewer)
- Adjust custom formatting templates

**Feed not updating in chat:**
- Check if RSS feed URL is accessible publicly
- Ensure the feed contains recent items
- Verify chat platform's polling interval (usually 15-60 minutes)

**Missing reviews:**
- Check FivestaRSS logs for API errors
- Verify API keys are working correctly
- Look for service alerts in the feed

## Update Frequency

Remember that FivestaRSS polls app stores every hour by default, so chat notifications will have a slight delay from when reviews are actually posted on the stores.
