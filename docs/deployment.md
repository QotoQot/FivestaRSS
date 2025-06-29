## Prerequisites

- [.NET 9 SDK installed](https://learn.microsoft.com/en-us/dotnet/core/install/linux)
- [Your API keys ready](docs/api-setup.md)

## Step 1: Clone and Build on Server

1. Clone the repository:
   ```bash
   cd /opt
   sudo git clone https://github.com/QotoQot/FivestaRSS.git fivestarss
   cd fivestarss
   ```

2. Build the application:
   ```bash
   dotnet build -c Release
   ```

3. Set up directories:
   ```bash
   mkdir -p keys feeds
   ```

4. Copy your API key files (`google-play-key.json` and `.p8`) to `/opt/fivestarss/keys/`

## Step 2: Configure for Production

1. Edit configuration:
   ```bash
   sudo nano appsettings.json
   ```

2. Update settings for production:
   ```json
   {
     "Kestrel": {
       "Endpoints": {
         "Http": {
           "Url": "http://0.0.0.0:5000"
         }
       }
     },
     "ApiKeys": {
       "GooglePlay": {
         "ServiceAccountKeyPath": "/opt/fivestarss/keys/google-play-key.json"
       },
       "AppStoreConnect": {
         "PrivateKeyPath": "/opt/fivestarss/keys/AuthKey_YOUR_KEY_ID.p8"
       }
     },
     "MonitoredApps": [
       {
         "Name": "Your App Name",
         "GooglePlayId": "com.yourcompany.yourapp",
         "AppStoreId": "123456789",
         "FeedFileName": "your-app.xml"
       }
     ]
   }
   ```

## Step 3: Create System Service

1. Create service file:
   ```bash
   sudo nano /etc/systemd/system/fivestarss.service
   ```

2. Add service configuration:
   ```ini
   [Unit]
   Description=FivestaRSS - App Review Monitor
   After=network.target

   [Service]
   Type=notify
   User=www-data
   Group=www-data
   WorkingDirectory=/opt/fivestarss
   ExecStart=/usr/bin/dotnet run --project FivestaRSS.csproj -c Release
   Restart=always
   RestartSec=10
   KillSignal=SIGINT
   
   # Environment variables for secrets
   Environment="ASPNETCORE_ENVIRONMENT=Production"
   Environment="ApiKeys__AppStoreConnect__IssuerId=YOUR_ISSUER_ID_HERE"
   Environment="ApiKeys__AppStoreConnect__KeyId=YOUR_KEY_ID_HERE"
   
   # Security settings
   NoNewPrivileges=true
   PrivateTmp=true
   ProtectSystem=strict
   ProtectHome=true
   ReadWritePaths=/opt/fivestarss/feeds

   [Install]
   WantedBy=multi-user.target
   ```

3. Replace placeholder values:
   - Change `YOUR_ISSUER_ID_HERE` to your App Store Issuer ID
   - Change `YOUR_KEY_ID_HERE` to your App Store Key ID

4. Set permissions:
   ```bash
   sudo chown -R www-data:www-data /opt/fivestarss
   sudo chmod -R 755 /opt/fivestarss
   sudo chmod 600 /opt/fivestarss/keys/*
   ```

## Step 4: Start the Service

1. Reload systemd and start:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable fivestarss
   sudo systemctl start fivestarss
   ```

2. Check status:
   ```bash
   sudo systemctl status fivestarss
   ```

## Step 5: Configure nginx Reverse Proxy for Feeds

Add a new `location` block for `/feeds/` inside your existing HTTPS-enabled `server` block. This ensures nginx will only proxy requests for feeds and all other routes remain as currently configured.

Open your nginx configuration for your domain:
```bash
sudo nano /etc/nginx/sites-available/your-site
```

Inside the `server` block for your site, add the following:
```nginx
location /feeds/ {
    proxy_pass http://localhost:5000/feeds/;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

After saving your changes, validate and reload nginx:
```bash
sudo nginx -t
sudo systemctl reload nginx
```

Try out `https://your-domain.com/feeds/your-app.xml` to see if it's working.

## Configuration Options

Edit `appsettings.json` to customize:

- `PollingIntervalMinutes`: Review check frequency (default: 60)
- `MaxReviewsPerFeed`: Maximum reviews per feed (default: 100)
- `FeedDirectory`: RSS file storage location (default: "feeds")
- `Url`: Server URL and port (default: "http://localhost:5000")

Restart the service with `sudo systemctl start fivestarss` to update the configuration.

### For updating
```bash
cd /opt/fivestarss
sudo systemctl stop fivestarss
sudo git pull
dotnet build -c Release
sudo systemctl start fivestarss
```

## Troubleshooting

**API key not found errors:**
- Ensure key files are in the `keys/` folder with correct names
- Verify environment secrets are properly configured

**Service won't start:**
- Check logs: `sudo journalctl -u fivestarss -n 50`
- Test manually: `cd /opt/fivestarss && dotnet run`

**Permission errors:**
- Ensure www-data owns the directory and can write to `/feeds/`
- Check API key file permissions (should be 600)

**Permission denied errors from API:**
- Google Play: Service account needs "View app information" permission
- App Store: API key requires "Developer" role

**Can't access feeds:**
- Check firewall: `sudo ufw allow 5000/tcp`
- Test locally: `curl http://localhost:5000/feeds/your-app.xml`

**No reviews appearing:**
- Verify app IDs in `appsettings.json` are correct
- Check console output for specific error messages
- In case of Google Play, only the reviews for the last 7 days can be fetched

**Port already in use error:**
- Change the port in `appsettings.json`

