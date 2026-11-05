# YouTube

[Türkçe](youtube.tr.md) · [← Back to the README](../../README.md#connect-your-accounts)

TM Publisher talks to YouTube only with **your own** developer app or key. Nothing goes through a server of ours; there is none.

## What you can post

One video per post (regular or Shorts) with title, description and tags. Uploads start private unless you choose another visibility.

## Return address

Register exactly this address in your app (the connection window shows it with a copy button):

```
http://127.0.0.1:47811/callback
```

## Steps

1. Create a project in Google Cloud (a free Google account is enough). <https://console.cloud.google.com/projectcreate>
2. Enable "YouTube Data API v3" in that project. <https://console.cloud.google.com/apis/library/youtube.googleapis.com>
3. Google Auth Platform → Branding: give the app a name and your e-mail address. <https://console.cloud.google.com/auth/branding>
4. Audience: choose "External" and click "Publish app" so it is "In production" (in "Testing" Google drops the access after 7 days). <https://console.cloud.google.com/auth/audience>
5. Clients → Create client → Application type "Desktop app"; copy the Client ID and the Client secret. <https://console.cloud.google.com/auth/clients>
6. Paste them here and click "Connect and test"; the browser opens Google's sign-in.
7. Choose the account of the channel. Google shows "Google hasn't verified this app" for your own unverified app: click "Advanced" → "Go to … (unsafe)" — it is your app.

When the test succeeds the account appears under **Accounts** and can be used in posts and groups.

## Good to know

- YouTube may keep videos uploaded by an unaudited project private; if so, apply for the API audit in the Google Cloud project or change the visibility in YouTube Studio.
- About 100 uploads per project per day; the quota resets at midnight Pacific Time.
- An unverified app can be used by up to 100 Google accounts: enough for you.
