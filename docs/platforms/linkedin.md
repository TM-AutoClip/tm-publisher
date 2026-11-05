# LinkedIn

[Türkçe](linkedin.tr.md) · [← Back to the README](../../README.md#connect-your-accounts)

TM Publisher talks to LinkedIn only with **your own** developer app or key. Nothing goes through a server of ours; there is none.

## What you can post

Text, photos or one video on your personal profile.

## Steps

1. Create an app on LinkedIn Developers (a LinkedIn Page is required as the app's company; your own page is fine). <https://www.linkedin.com/developers/apps/new>
2. Products: request "Share on LinkedIn" and "Sign In with LinkedIn using OpenID Connect" (both are granted at once).
3. Open the OAuth token generator, choose your app and the scopes openid, profile and w_member_social, then request the token. <https://www.linkedin.com/developers/tools/oauth/token-generator>
4. Copy the access token and paste it here.
5. Click "Connect and test".

When the test succeeds the account appears under **Accounts** and can be used in posts and groups.

## Good to know

- The token lasts 60 days and LinkedIn does not let personal apps renew it automatically: connect again every 60 days (the account list reminds you a week before).
