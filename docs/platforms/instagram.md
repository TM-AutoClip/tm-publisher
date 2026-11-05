# Instagram

[Türkçe](instagram.tr.md) · [← Back to the README](../../README.md#connect-your-accounts)

TM Publisher talks to Instagram only with **your own** developer app or key. Nothing goes through a server of ours; there is none.

## What you can post

Videos (Reels) only in this version, caption up to 2,200 characters and 5 hashtags.

## Steps

1. Make the Instagram account a professional (Business or Creator) account and link it to a Facebook Page you manage. <https://help.instagram.com/502981923235522>
2. On Meta for Developers create an app (use case "Other", type "Business"); App settings → Basic: copy the App ID and App Secret. <https://developers.facebook.com/apps/creation/>
3. Add the "Instagram" product (API setup with Facebook Login) to the app.
4. In Graph API Explorer get a user token with instagram_basic, instagram_content_publish, pages_show_list, pages_read_engagement and business_management. <https://developers.facebook.com/tools/explorer/>
5. Choose the Page and the Instagram account when Facebook asks, then copy the token.
6. Paste everything here and click "Connect and test".

When the test succeeds the account appears under **Accounts** and can be used in posts and groups.

## Good to know

- Videos (Reels) only in this version: Instagram takes photos only from a public web address.
- 100 posts in 24 hours, at most 5 hashtags, caption up to 2,200 characters, video up to 300 MB.
- Your app stays in development mode. Check once from another account that a test post is visible to others; if not, switch the app to Live (Meta may ask for a privacy policy address).
