# Pinterest

[Türkçe](pinterest.tr.md) · [← Back to the README](../../README.md#connect-your-accounts)

TM Publisher talks to Pinterest only with **your own** developer app or key. Nothing goes through a server of ours; there is none.

## What you can post

One image or one video pin with title, description and an optional link, to the board you choose.

## Return address

Register exactly this address in your app (the connection window shows it with a copy button):

```
https://localhost/callback
```

## Steps

1. On Pinterest Developers connect your app (a business account is recommended). <https://developers.pinterest.com/apps/>
2. Add this return address (copy it) to the app's redirect URIs. Any https address works; the page does not need to load.
3. Copy the App id and the App secret key.
4. Paste them here, optionally write the board, and click "Connect and test".
5. After allowing access the browser lands on an address that does not load: copy it from the address bar and paste it into the box here.

When the test succeeds the account appears under **Accounts** and can be used in posts and groups.

## Good to know

- With Trial access your pins are visible only to you. Standard access needs a short video showing the app's sign-in, sent to Pinterest for review.
- Apps created on or after 14 September 2026 may get "403" for personal (non-business) accounts; use a business account.
