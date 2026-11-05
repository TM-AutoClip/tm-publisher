# X

[Türkçe](x.tr.md) · [← Back to the README](../../README.md#connect-your-accounts)

TM Publisher talks to X only with **your own** developer app or key. Nothing goes through a server of ours; there is none.

## What you can post

Text (280 weighted characters), up to 4 photos or one video. X charges per post: about $0.015, $0.20 with a link.

## Return address

Register exactly this address in your app (the connection window shows it with a copy button):

```
http://127.0.0.1:47812/callback
```

## Steps

1. Open the X developer console and sign in with the X account that will post. <https://console.x.com>
2. Create a Project and an App. In the app's "User authentication settings" choose OAuth 2.0, type "Native App", permissions "Read and write".
3. Add this exact Callback URI (copy it) and any website address, then save.
4. Copy the OAuth 2.0 Client ID (a native app has no secret).
5. X is pay per use: add credits to the developer account before posting. <https://docs.x.com/x-api/getting-started/pricing>
6. Paste the Client ID and click "Connect and test"; allow access in the browser.

When the test succeeds the account appears under **Accounts** and can be used in posts and groups.

## Good to know

- X charges per post (about $0.015; a post with a link about $0.20). The compose screen shows the estimate before sending.
