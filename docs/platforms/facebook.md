# Facebook Page

[Türkçe](facebook.tr.md) · [← Back to the README](../../README.md#connect-your-accounts)

TM Publisher talks to Facebook Page only with **your own** developer app or key. Nothing goes through a server of ours; there is none.

## What you can post

Text, links, photos and one video on a Facebook Page you manage.

## Steps

1. On Meta for Developers create an app (use case "Other", type "Business"). It is your own app; it stays in development mode. <https://developers.facebook.com/apps/creation/>
2. App settings → Basic: copy the App ID and the App Secret.
3. Open Graph API Explorer, pick your app, choose "Get User Access Token" and add pages_show_list, pages_read_engagement and pages_manage_posts. <https://developers.facebook.com/tools/explorer/>
4. Click "Generate Access Token", choose your Page when Facebook asks, then copy the token.
5. Paste the App ID, App Secret and token here and click "Connect and test": TM Publisher takes the Page's token, which does not expire.

When the test succeeds the account appears under **Accounts** and can be used in posts and groups.

## Good to know

- Your app stays in development mode. Check once from another account that a test post is visible to others; if not, switch the app to Live (Meta may ask for a privacy policy address).
