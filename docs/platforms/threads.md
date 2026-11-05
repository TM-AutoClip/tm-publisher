# Threads

[Türkçe](threads.tr.md) · [← Back to the README](../../README.md#connect-your-accounts)

TM Publisher talks to Threads only with **your own** developer app or key. Nothing goes through a server of ours; there is none.

## What you can post

Text only in this version (500 characters).

## Return address

Register exactly this address in your app (the connection window shows it with a copy button):

```
https://localhost/callback
```

## Steps

1. On Meta for Developers create an app with the use case "Access the Threads API". <https://developers.facebook.com/apps/creation/>
2. App roles → Roles → add your Threads account as a "Threads Tester", then accept the invite in Threads (Settings → Account → Website permissions).
3. Use cases → Threads API → Settings: add this return address to "Redirect Callback URLs" (copy it) and save.
4. Copy the Threads App ID and the Threads App Secret from the same page.
5. Paste them here and click "Connect and test"; allow access in the browser.
6. The browser then lands on an address that does not load: copy the whole address from the address bar and paste it into the box here.

When the test succeeds the account appears under **Accounts** and can be used in posts and groups.

## Good to know

- Text only in this version: Threads takes photos and videos only from a public web address.
- Access lasts 60 days and TM Publisher renews it when it is used; if the app is not used for that long, connect again.
