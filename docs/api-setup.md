# API Keys Setup

## App Store Connect API

1. In [App Store Connect](https://appstoreconnect.apple.com/), go to **Users and Access** → **Keys** tab.
2. Generate a new key with [**Developer** access](https://developer.apple.com/help/account/access/roles/). This will provide:
   - *Issuer ID*
   - *Key ID*
   - A private key file (`.p8`) for download  
   **Note:** You can only download the key once.

See [Apple’s official documentation](https://developer.apple.com/documentation/appstoreconnectapi/creating-api-keys-for-app-store-connect-api) for reference.

## Google Play API

1. **Set up a Google Cloud Project**  
   If you don't have one, create a new project in the [Google Cloud Console](https://console.cloud.google.com/).

2. **Enable the API**  
   In your Cloud Project:
   - Go to **APIs & Services** → **Library**
   - Search for and enable the **Google Play Android Developer API**

3. **Create Service Account Credentials**
   - Navigate to **APIs & Services** → **Credentials**
   - Click **Create Credentials** → **Service Account**
   - Give it a name (e.g., `play-reviews-reader`)
   - Skip role assignment
   - After creation, go to the **Keys** tab
   - Click **Add Key** → **Create new key**, choose **JSON**, and download the private key file
   - Copy the service account’s email from the **Details** tab

4. **Link Service Account in Google Play Console**
   - Go to the [Google Play Console](https://play.google.com/console/)
   - Navigate to **Users and permissions**
   - Click **Invite new users**, and paste the copied service account email
   - Under **Account permissions**, grant:  
     *View app information and download bulk reports (read-only)*
   - Click **Invite user**

   Alternatively, you can grant access to a specific app instead of the entire account.

## Security Note

Both Apple’s `.p8` and Google’s `.json` private keys contain sensitive credentials. **Do not commit them to version control or share them.**

## Troubleshooting API Setup

### Google Play Console Issues
- Verify the service account has correct permissions
- Check that the JSON key file is valid and uncorrupted
- Ensure the service account is linked to the correct Play Console account

### App Store Connect Issues
- Confirm the API key has not expired
- Verify the Issuer ID and Key ID are correctly configured
- Ensure the key has the **Developer** role

### File Path Issues
- Key files should be placed in the `keys/` directory relative to the application
- File names must match exactly as specified in your config
- File permissions should allow the application to read the key files
