# Discord

[Türkçe](discord.tr.md) · [← Back to the README](../../README.md#connect-your-accounts)

TM Publisher talks to Discord only with **your own** developer app or key. Nothing goes through a server of ours; there is none.

## What you can post

Text and files to a channel through a webhook.

## Steps

1. In Discord open the channel's settings → Integrations → Webhooks → New Webhook (you need the Manage Webhooks right). <https://support.discord.com/hc/articles/228383668>
2. Give it a name and a picture, check the channel, then click "Copy Webhook URL".
3. Paste the address into "Webhook address".
4. Click "Connect and test": TM Publisher reads the webhook's name.

When the test succeeds the account appears under **Accounts** and can be used in posts and groups.

## Good to know

- Anyone with the webhook address can post to the channel: keep it secret (TM Publisher stores it encrypted).
- Up to 10 files, 20 MB each and 25 MB per post; text up to 2,000 characters.
