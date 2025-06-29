# API Keys Setup

## App Store Connect API

1. In [App Store Connect](https://appstoreconnect.apple.com/), go to "Users and Access" -> "Keys" tab.
2. Generate a new key with "Developer" access. https://developer.apple.com/help/account/access/roles/ This will give you an *Issuer ID*, a *Key ID*, and let you download a private key file (`.p8`). **You can only download the key once.** 

[The official documentation from Apple](https://developer.apple.com/documentation/appstoreconnectapi/creating-api-keys-for-app-store-connect-api)

## Google Play API

1.**Set up a Google Cloud Project:** If you don't have one, create a new project in the [Google Cloud Console](https://console.cloud.google.com/).
2.**Enable the API:** In your Cloud Project, go to "APIs & Services" > "Library" and search for and enable the "Google Play Android Developer API".
3.**Create Service Account Credentials:**
* In the sidebar, go to "APIs & Services" > "Credentials".
* Click "Create Credentials" -> "Service Account" at the top.
* Give it a name (e.g., "play-reviews-reader").
* You can skip granting it any role.
* After creating it, save click on it and go to the "Keys" tab. Click "Add Key" -> "Create new key", and choose *JSON*. A private key file will download instantly.
* Copy its email from the "Details" tab.
4.**Link Service Account in Google Play Console:**
* Go to your [Google Play Console](https://play.google.com/console/).
* Navigate to "Users and permissions" in the sidebar.
* Click "Invite new users" and paste the previously copied email of the Service Account.
* Under the "Account permissions" tab, grant the account the *"View app information and download bulk reports (read-only)"* permission.
* Click "Invite user".

Alternatively, you can only give it permission for a specicif app instead of the whole account.

## Security Note

Both Apple's `p8` file and Google's `.json` contain sensitive credentials and should never be committed to version control or shared somewhere else.

## Troubleshooting API Setup

**Google Play Console Issues:**
- Verify service account has correct permissions
- Check that the JSON key file is valid and not corrupted
- Ensure the service account is linked to the correct Google Play Console account

**App Store Connect Issues:**
- Confirm the API key has not expired
- Verify the Issuer ID and Key ID are correctly configured
- Check that the role assigned has "Developer" access

**File Path Issues:**
- Ensure key files are placed in the `keys/` directory relative to the application
- Verify file names match exactly what is specified in configuration files
- Check file permissions allow the application to read the key files
