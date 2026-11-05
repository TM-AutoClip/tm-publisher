# TikTok

[Türkçe](tiktok.tr.md) · [← Back to the README](../../README.md#connect-your-accounts)

TM Publisher talks to TikTok only with **your own** developer app or key. Nothing goes through a server of ours; there is none.

## What you can post

One video per post. By default it goes to your TikTok inbox as a draft; an optional direct post is always private and needs a private TikTok account.

## Return address

Register exactly this address in your app (the connection window shows it with a copy button):

```
http://127.0.0.1:47813/callback
```

## Steps

1. Sign in to TikTok for Developers and create an app (Manage apps → Connect an app). <https://developers.tiktok.com/apps/>
2. Add the products "Login Kit" (platform: Desktop) and "Content Posting API"; turn on "Direct Post" if you want the private direct option.
3. In Login Kit add this exact redirect address (copy it).
4. Add the scopes user.info.basic, video.upload and video.publish; for a sandbox, add your TikTok account as a target user.
5. Copy the Client key and the Client secret.
6. Paste them here and click "Connect and test"; allow access in the browser.

When the test succeeds the account appears under **Accounts** and can be used in posts and groups.

## Good to know

- By default the video goes to your TikTok inbox as a draft; you finish the post (caption, visibility) in the TikTok app. Drafts carry no text.
- Direct posting is optional and, for an app that TikTok has not audited, always private (only you can see it). TikTok also allows it only when the TikTok account itself is private (TikTok app → Settings → Privacy → Private account); with a public account use drafts.
- TikTok's audit does not accept "a tool to upload to my own accounts", so public direct posts stay closed; at most 5 drafts may wait in 24 hours.
