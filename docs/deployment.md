## Prerequisites

- [.NET 9 SDK installed locally](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Your API keys ready](docs/api-setup.md)
- SSH access to your server

## Step 1: Build Locally

1. Clone the repository on your local machine:
   ```bash
   git clone https://github.com/QotoQot/FivestaRSS.git
   cd FivestaRSS/src
   ```

2. Build a self-contained release (no .NET runtime needed on server):
   ```bash
   dotnet publish -c Release -r linux-x64 --self-contained true -o ../publish
   ```
   This creates a fully self-contained binary in the `publish` folder.

3. Create deployment package:
   ```bash
   cd ../publish
   tar -czf fivestarss.tar.gz *
   ```

## Step 2: Deploy to Server

1. Upload the package to your server:
   ```bash
   rsync -avz --progress fivestarss.tar.gz user@yourserver:/tmp/
   ```
   (Alternative: `scp fivestarss.tar.gz user@yourserver:/tmp/`)

2. On your server, create application directory:
   ```bash
   sudo mkdir -p /opt/fivestarss
   ```

3. Extract the application:
   ```bash
   cd /opt/fivestarss
   sudo tar -xzf /tmp/fivestarss.tar.gz
   rm /tmp/fivestarss.tar.gz
   ```

4. Create required directories:
   ```bash
   sudo mkdir -p keys feeds
   ```

5. Make the binary executable:
   ```bash
   sudo chmod +x FivestaRSS
   ```

## Step 3: Configure the Application

1. Upload API key files from your computer:
   ```bash
   # Upload to /tmp first (you have write permissions there)
   rsync -avz --progress google-play-key.json user@yourserver:/tmp/
   rsync -avz --progress AuthKey_YOUR_KEY_ID.p8 user@yourserver:/tmp/
   ```

2. Then on your server move them to the FivestaRSS' directory:
   ```bash
   ssh user@yourserver
   sudo mv /tmp/google-play-key.json /opt/fivestarss/keys/
   sudo mv /tmp/AuthKey_YOUR_KEY_ID.p8 /opt/fivestarss/keys/
   ```

3. Edit the configuration:
   ```bash
   nano appsettings.json
   ```

Google's key path should be correct already, so set:
 - App Store Connect's private key path and its two IDs
 - App name, two store IDs, and XML filename
 - (Optional) BaseUrl in FeedSettings if you want the RSS feed to include a channel link. Example:
   ```json
   "FeedSettings": {
     "BaseUrl": "https://yoursite.com",
     ...
   }
   ```
   If not set, the RSS feed will not include a channel link element, which is valid per RSS 2.0 spec.

## Step 4: Create System Service

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
   Type=simple
   User=www-data
   Group=www-data
   WorkingDirectory=/opt/fivestarss
   ExecStart=/opt/fivestarss/FivestaRSS
   Restart=always
   RestartSec=10
   KillSignal=SIGINT
   TimeoutStartSec=60
   
   # Environment
   Environment="ASPNETCORE_ENVIRONMENT=Production"
   
   # Security settings
   NoNewPrivileges=true
   PrivateTmp=true
   ProtectSystem=strict
   ProtectHome=true
   ReadWritePaths=/opt/fivestarss/feeds

   [Install]
   WantedBy=multi-user.target
   ```

3. Set permissions:
   ```bash
   sudo chown -R www-data:www-data /opt/fivestarss
   sudo chmod -R 755 /opt/fivestarss
   sudo chmod 600 /opt/fivestarss/keys/*
   ```

## Step 5: Start the Service

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

## Step 6: Configure nginx Reverse Proxy

Add to your nginx site configuration (somewhere in `/etc/nginx/sites-enabled`):

```nginx
location /feeds/ {
    proxy_pass http://localhost:5000/feeds/;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

Test and reload nginx:
```bash
sudo nginx -t
sudo systemctl reload nginx
```

**Done**, check `yoursite.com/feeds/yourapp.xml`

## Updating the Application

To update to a new version:

1. Build locally:
   ```bash
   cd FivestaRSS/src
   git pull
   dotnet publish -c Release -r linux-x64 --self-contained true -o ../publish
   cd ../publish
   tar -czf fivestarss.tar.gz *
   ```

2. Deploy with config backup:
   ```bash
   rsync -avz --progress fivestarss.tar.gz user@yourserver:/tmp/
   ssh user@yourserver
   sudo systemctl stop fivestarss
   cd /opt/fivestarss
   
   # Backup current config
   sudo cp appsettings.json appsettings.json.backup
   
   # Extract new version
   sudo tar -xzf /tmp/fivestarss.tar.gz
   
   # Restore config
   sudo mv appsettings.json.backup appsettings.json
   
   sudo chmod +x FivestaRSS
   sudo chown -R www-data:www-data /opt/fivestarss
   sudo systemctl start fivestarss
   ```

## Troubleshooting

**Service won't start:**
- Check logs: `sudo journalctl -u fivestarss -n 50`
- Test manually: `cd /opt/fivestarss && sudo -u www-data ./FivestaRSS`
- Verify the binary is executable: `ls -la FivestaRSS`

**Permission errors:**
- Ensure www-data owns all files and can write to feeds directory
- Check API key file permissions (should be 600)

**Can't access feeds:**
- Check firewall: `sudo ufw allow 5000/tcp`
- Test locally: `curl http://localhost:5000/feeds/your-app.xml`

**API errors:**
- Verify API keys are correctly placed in `/opt/fivestarss/keys/`
- Check IssuerId and KeyId in appsettings.json
- Google Play: Service account needs "View app information" permission
- App Store: API key requires "Developer" role
