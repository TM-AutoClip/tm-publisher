<p align="center"><img src="assets/brand/tm-publisher-logo-horizontal-dark.svg" alt="TM Publisher" width="420"></p>

**English** · [Türkçe](README.tr.md)

TM Publisher is a free, open-source (MIT) desktop app that publishes one post to many social accounts from your own computer.
It runs on Windows and Linux, keeps everything in an encrypted local database and talks to the platforms only with
**your own developer apps and keys**. There is no server, no shared app and no account with us.

Supported: YouTube · X · TikTok · Instagram · Facebook Page · Threads · Bluesky · Mastodon · Telegram · Discord · Pinterest · LinkedIn.

- [What it does](#what-it-does)
- [Install on Windows](#install-on-windows)
- [Install on Linux (x64 and arm64)](#install-on-linux-x64-and-arm64)
- [First start](#first-start)
- [Connect your accounts](#connect-your-accounts)
- [Update](#update)
- [Uninstall](#uninstall)
- [Where your data is](#where-your-data-is)
- [Known limits](#known-limits)
- [Build from source](#build-from-source)

## What it does

- **Write once, send everywhere.** Text, photos or a video; each account shows exactly the text it will receive (platform
  tags, title, length counter) and the same checks that run before sending. What the form shows is what is published.
- **Groups.** Send to a saved group of accounts in its order, or pick accounts by hand. A post never goes to nobody.
- **Schedule.** One timer, no polling. Posts are sent one account at a time; if one fails, the rest wait for your decision
  (try again, skip and continue, cancel). A post up to 5 minutes late is still sent; a post missed while the computer was
  off waits for you on the **Waiting for you** page.
- **Calendar.** Month and week views; drag a planned post to another day.
- **History.** Links to what was published, local counts and limits. Platform numbers (views, likes) are read only when you
  click **Fetch counters**, and X's read cost is shown first.
- **Notifications** in the bottom-right corner (published, needs your approval, error). One minute by default; you can change
  the time or turn them off in Settings.
- **Encrypted.** The database is encrypted (SQLite3 Multiple Ciphers). Its key is protected by Windows (DPAPI) or the Linux
  keyring, or by a master password you choose. Backups are a single password-protected file (Argon2id + AES-256-GCM).
- **Two languages:** English and Turkish.

It does **not** read messages or comments, and it does not send e-mail.

## Install on Windows

Windows 10 or 11, 64-bit.

1. Open the [latest release](../../releases/latest) and download `TMPublisher-<version>-win-x64.exe`.
2. Optional but recommended: check the file against the SHA-256 value on the release page. In PowerShell:
   ```powershell
   Get-FileHash .\TMPublisher-0.1.0-win-x64.exe -Algorithm SHA256
   ```
3. Put the file where it can stay, for example `%LOCALAPPDATA%\Programs\TM Publisher\TMPublisher.exe`. Autostart points to
   this exact path, so choose the place before the first start.
4. Double-click it. The file is not code-signed, so Windows shows **"Windows protected your PC"** (SmartScreen):
   click **More info**, then **Run anyway**. Windows usually asks only the first time.
5. Continue with [First start](#first-start).

No administrator rights are needed; nothing is installed system-wide.

## Install on Linux (x64 and arm64)

Use `linux-x64` on ordinary PCs and `linux-arm64` on 64-bit ARM computers (for example a Raspberry Pi 4 or 5 with a 64-bit
desktop). A desktop session (X11, or Wayland with XWayland) is required.

1. Download `tm-publisher-<version>-linux-x64.tar.gz` (or `-linux-arm64`) from the [latest release](../../releases/latest).
2. Optional: check the SHA-256 value:
   ```sh
   sha256sum tm-publisher-0.1.0-linux-x64.tar.gz
   ```
3. Unpack it where it can stay and allow it to run:
   ```sh
   mkdir -p ~/.local/opt
   tar -xzf tm-publisher-0.1.0-linux-x64.tar.gz -C ~/.local/opt
   chmod +x ~/.local/opt/tm-publisher/tm-publisher
   ```
4. **Keyring (recommended).** To open the database automatically when you sign in, TM Publisher stores its key in your
   desktop keyring (GNOME Keyring, KWallet, KeePassXC...) with the `secret-tool` command:
   - Debian, Ubuntu, Raspberry Pi OS: `sudo apt install libsecret-tools`
   - Fedora: `sudo dnf install libsecret`
   - Arch: `sudo pacman -S libsecret`

   Without a keyring the app still works: it protects the database with a master password that it asks at every start.
5. Start it: `~/.local/opt/tm-publisher/tm-publisher`, then continue with [First start](#first-start).

Notes:

- **Tray icon:** needs StatusNotifierItem / AppIndicator support. KDE, XFCE, Cinnamon and most panels have it; on GNOME install
  the "AppIndicator and KStatusNotifierItem Support" extension. Without a tray, closing the window minimizes it instead.
- **Autostart:** the app writes `~/.config/autostart/tm-publisher.desktop` (desktops that follow the XDG autostart
  specification start it at sign-in). Turn it on or off in Settings.
- If the program does not start and mentions ICU, install your distribution's `libicu` package.

## First start

1. **Language:** English or Turkish (can be changed later in Settings).
2. **Start fresh** or **Restore from a backup** (a `.tmpbak` file and its password).
3. **Protection:** by default the database key is protected by this computer (Windows) or the keyring (Linux) and opens
   automatically. You can choose a **master password** instead; it is then asked at every start. A forgotten master password
   cannot be recovered, so keep a backup.
4. **Start with my computer (in the tray)** is on by default: TM Publisher starts in the tray so scheduled posts go out on time. Turn it
   off in Settings if you prefer.
5. Add your accounts under **Accounts** (see below), optionally make **Groups**, then write your first post under **New post**.

Make a backup in **Settings → Backup** after connecting your accounts. The backup holds the encrypted database (accounts,
keys, groups, posts, settings), not your media files.

## Connect your accounts

Each platform is connected with **your own** developer app or key. The **Add an account** window shows the same steps as these
guides, each with a checkbox, the return address to copy where one is needed, and a **Connect and test** button that tells
you at once whether it worked.

| Platform | What you can post | How it connects | Guide |
|---|---|---|---|
| YouTube | One video | Your Google Cloud "Desktop app" client | [English](docs/platforms/youtube.md) · [Türkçe](docs/platforms/youtube.tr.md) |
| X | Text, up to 4 photos or a video (paid per post) | Your X app (OAuth 2.0, no secret) | [English](docs/platforms/x.md) · [Türkçe](docs/platforms/x.tr.md) |
| TikTok | One video, as a draft in your TikTok inbox (or private) | Your TikTok app (Login Kit for Desktop) | [English](docs/platforms/tiktok.md) · [Türkçe](docs/platforms/tiktok.tr.md) |
| Instagram | One video (Reels) | Your Meta app + a professional account linked to a Page | [English](docs/platforms/instagram.md) · [Türkçe](docs/platforms/instagram.tr.md) |
| Facebook Page | Text, links, photos, one video | Your Meta app + Graph API Explorer token | [English](docs/platforms/facebook.md) · [Türkçe](docs/platforms/facebook.tr.md) |
| Threads | Text | Your Threads app (paste the return address) | [English](docs/platforms/threads.md) · [Türkçe](docs/platforms/threads.tr.md) |
| Bluesky | Text, up to 4 photos or a video | An app password | [English](docs/platforms/bluesky.md) · [Türkçe](docs/platforms/bluesky.tr.md) |
| Mastodon | Text, up to 4 photos or a video | The app registers itself on your server | [English](docs/platforms/mastodon.md) · [Türkçe](docs/platforms/mastodon.tr.md) |
| Telegram | Text, photos, videos, albums | Your bot (BotFather) | [English](docs/platforms/telegram.md) · [Türkçe](docs/platforms/telegram.tr.md) |
| Discord | Text and files | A channel webhook | [English](docs/platforms/discord.md) · [Türkçe](docs/platforms/discord.tr.md) |
| Pinterest | One image or video pin | Your Pinterest app (paste the return address) | [English](docs/platforms/pinterest.md) · [Türkçe](docs/platforms/pinterest.tr.md) |
| LinkedIn | Text, photos or a video on your profile | Token from LinkedIn's token generator (60 days) | [English](docs/platforms/linkedin.md) · [Türkçe](docs/platforms/linkedin.tr.md) |

## Update

1. Optional: make a backup in **Settings → Backup**.
2. Quit TM Publisher from its tray menu (**Quit**); closing the window only hides it.
3. Replace the program file with the new one (same place and name, so autostart keeps working). On Linux unpack the new
   archive over the old folder and run `chmod +x` again.
4. Start it. Your data stays where it was.

## Uninstall

1. In **Settings**, turn off **Start with my computer** (this removes the Windows autostart entry or disables the Linux one).
2. Quit from the tray menu.
3. Delete the program file (Windows) or the `tm-publisher` folder (Linux), and on Linux
   `~/.config/autostart/tm-publisher.desktop`.
4. To remove your data too, delete the data folder below. This cannot be undone; make a backup first if you may need it.
   On Linux with the keyring, also delete the "TM Publisher database key" entry in your keyring app (Seahorse, KWallet...).

## Where your data is

| | Folder |
|---|---|
| Windows | `%LOCALAPPDATA%\TMPublisher` |
| Linux | `~/.local/share/tm-publisher` (or `$XDG_DATA_HOME/tm-publisher`) |

Inside: `publisher.db` (the encrypted database), `key.json` (the database key, sealed by DPAPI, the keyring or your master
password), `preferences.json` (only the interface language), `logs/` (kept 14 days; never contain tokens or passwords).
Media files stay where you keep them; only their paths are stored.

Another folder can be used with `--data-dir <folder>` or the `TMPUBLISHER_DATA_DIR` environment variable. The program
unpacks its native libraries to the system's temporary folder on first start (standard .NET single-file behavior).

## Known limits

- **TikTok:** posts go to your TikTok inbox as drafts and you finish them in the app; the optional direct post is private.
  TikTok does not audit "upload to my own account" tools, so public direct posting is closed.
- **Pinterest:** with Trial access your pins are visible only to you; Standard access needs Pinterest's review of your app.
- **Instagram and Threads:** videos (Instagram) and text (Threads) only in this version; their photos must come from a public
  web address.
- **LinkedIn:** the token lasts 60 days; connect again when the app reminds you.
- **X** charges per post (about $0.015, $0.20 with a link) and per read; the compose screen and the Fetch counters button show the
  estimate first.
- No message or comment reading, no e-mail.

## Build from source

Requires the .NET 10 SDK and Python 3 (for the git hooks).

```sh
git clone <this repository>
cd tm-publisher
tools/install-hooks.sh        # or: pwsh tools/install-hooks.ps1
dotnet test TMPublisher.slnx
dotnet run --project src/TMPublisher.Desktop
```

Release files: `pwsh tools/publish.ps1` or `tools/publish.sh` (self-contained single files in `artifacts/release`, with
SHA-256 values printed). Platform guides in `docs/platforms` are generated from the app's own texts with
`python tools/gen-platform-docs.py`.

## License

MIT, see [LICENSE](LICENSE). Third-party material: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
