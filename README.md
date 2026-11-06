<p align="center"><img src="assets/brand/tm-publisher-logo-horizontal-dark.svg" alt="TM Publisher" width="420"></p>

**English** · [Türkçe](README.tr.md)

TM Publisher is a free, open-source (MIT) desktop app that publishes one post to many social accounts from your own computer.
It runs on Windows and Linux, keeps everything in an encrypted local database and talks to the platforms only with
**your own developer apps and keys**. There is no server, no shared app and no account with us.

Supported: YouTube · X · TikTok · Instagram · Facebook Page · Threads · Bluesky · Mastodon · Telegram · Discord · Pinterest · LinkedIn.

- [What it does](#what-it-does)
- [What runs in the background](#what-runs-in-the-background)
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
- **Schedule.** A small background scheduler sends your posts at their time, also when the window is closed (see
  [What runs in the background](#what-runs-in-the-background)). One timer, no polling. Posts are sent one account at a time;
  if one fails, the rest wait for your decision (try again, skip and continue, cancel). A post up to 5 minutes late is still
  sent; a post missed while the computer was off waits for you on the **Waiting for you** page.
- **Calendar.** Month and week views; drag a planned post to another day.
- **History.** Links to what was published, local counts and limits. Platform numbers (views, likes) are read only when you
  click **Fetch counters**, and X's read cost is shown first.
- **Notifications** in the bottom-right corner (published, needs your approval, error), also when the window is closed. One
  minute by default; you can change the time or turn them off in Settings.
- **Encrypted.** The database is encrypted (SQLite3 Multiple Ciphers). Its key is protected by Windows (DPAPI) or the Linux
  keyring, or by a master password you choose. Backups are a single password-protected file (Argon2id + AES-256-GCM).
- **Two languages:** English and Turkish.

It does **not** read messages or comments, and it does not send e-mail.

## What runs in the background

TM Publisher is two programs in one folder:

| | Windows | Linux | Runs |
|---|---|---|---|
| The window | `TMPublisher.exe` | `tm-publisher` | only while you have it open; closing it ends it |
| The background scheduler | `TMPublisherScheduler.exe` | `tm-publisher-scheduler` | all the time: it waits for the next post's time and sends it |

The scheduler is small: no window library, compiled to native code. Measured for 0.1.3 while it waits for posts: about 6 MB in
Windows Task Manager (22 MB working set), about 22 MB resident on a Raspberry Pi 5. While it sends a post it uses more for a
moment, and a notification is shown by a short-lived copy of the window program that ends when the notification closes.

- **It starts** with your computer (Settings → **Start the background scheduler with my computer**, on by default) and whenever
  you open the window. Only one scheduler runs per data folder.
- **See and control it:** Settings → **Background scheduler** shows whether it runs, the next post, and has Start, Stop, Pause and
  Resume. On Windows it also has a tray icon: click it to open the window; right-click for Pause / Resume scheduling and Quit.
- **Stop it:** Settings → **Stop**, or the tray icon → **Quit (stop scheduling)** on Windows. Posts are not sent while it is
  stopped; it starts again with the window or the computer. To keep it from starting with the computer, turn the setting off.
  A post being sent is finished first.
- **Pause** keeps it running but sends nothing; posts whose time passes more than 5 minutes before you resume wait for you.
- **Master password:** if you protect the database with a master password (always the case on Linux without a keyring, for
  example on a Raspberry Pi), the scheduler cannot ask for it. After the computer starts, open TM Publisher once and unlock it:
  the window hands the key to the scheduler over a channel only your user can open (never on a command line or in a file). Until
  then Settings shows "Waiting for the master password" and nothing is sent.

## Install on Windows

Windows 10 or 11, 64-bit.

1. Open the [latest release](../../releases/latest) and download `TMPublisher-<version>-win-x64.zip`.
2. Optional but recommended: check the file against the SHA-256 value on the release page. In PowerShell:
   ```powershell
   Get-FileHash .\TMPublisher-0.1.3-win-x64.zip -Algorithm SHA256
   ```
3. Unpack it where it can stay, for example to `%LOCALAPPDATA%\Programs`. You get a `TM Publisher` folder with
   `TMPublisher.exe`, `TMPublisherScheduler.exe` and `sqlite3mc.dll`; keep the three together. Autostart points to this exact
   folder, so choose the place before the first start.
4. Double-click `TMPublisher.exe`. The files are not code-signed, so Windows shows **"Windows protected your PC"**
   (SmartScreen): click **More info**, then **Run anyway**. Windows may ask once more for the scheduler.
5. Continue with [First start](#first-start).

No administrator rights are needed; nothing is installed system-wide.

## Install on Linux (x64 and arm64)

Use `linux-x64` on ordinary PCs and `linux-arm64` on 64-bit ARM computers (for example a Raspberry Pi 4 or 5 with a 64-bit
desktop). A desktop session (X11, or Wayland with XWayland) is required for the window and the notifications.

1. Download `tm-publisher-<version>-linux-x64.tar.gz` (or `-linux-arm64`) from the [latest release](../../releases/latest).
2. Optional: check the SHA-256 value:
   ```sh
   sha256sum tm-publisher-0.1.3-linux-x64.tar.gz
   ```
3. Unpack it where it can stay (the programs are already marked executable):
   ```sh
   mkdir -p ~/.local/opt
   tar -xzf tm-publisher-0.1.3-linux-x64.tar.gz -C ~/.local/opt
   ```
4. **Keyring (recommended).** To open the database automatically when you sign in, TM Publisher stores its key in your
   desktop keyring (GNOME Keyring, KWallet, KeePassXC...) with the `secret-tool` command:
   - Debian, Ubuntu, Raspberry Pi OS: `sudo apt install libsecret-tools`
   - Fedora: `sudo dnf install libsecret`
   - Arch: `sudo pacman -S libsecret`

   Without a keyring the app still works: it protects the database with a master password, and you unlock the window once after
   each start of the computer (see [What runs in the background](#what-runs-in-the-background)).
5. Start it: `~/.local/opt/tm-publisher/tm-publisher`, then continue with [First start](#first-start). A menu entry for the
   window is up to you (for example a `.desktop` file in `~/.local/share/applications` whose `Exec=` line is the full path of
   `tm-publisher`).

Notes:

- **No tray icon on Linux:** the scheduler has none (a tray host is not there on every desktop). Open the window from your menu
  or the command line; Settings → Background scheduler shows its state and stops it.
- **Autostart:** the window writes `~/.config/autostart/tm-publisher.desktop`, which starts the scheduler at sign-in on desktops
  that follow the XDG autostart specification. Turn it on or off in Settings.
- **linux-x64:** the scheduler for x64 is not compiled to native code in this release (that is only possible on an x64 Linux
  machine, which the release was not built on); it is a trimmed, precompiled single file instead and uses about twice the memory
  of the arm64 one (measured on arm64: about 46 MB instead of 22 MB). Everything else is the same.

## First start

1. **Language:** English or Turkish (can be changed later in Settings).
2. **Start fresh** or **Restore from a backup** (a `.tmpbak` file and its password).
3. **Protection:** by default the database key is protected by this computer (Windows) or the keyring (Linux) and opens
   automatically. You can choose a **master password** instead; it is then asked at every start. A forgotten master password
   cannot be recovered, so keep a backup.
4. **Start the background scheduler with my computer** is on by default, so scheduled posts go out on time also when the window
   is closed. Turn it off in Settings if you prefer.
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
2. Stop the old version:
   - **From 0.1.0–0.1.2:** quit it from its tray menu (**Quit**) or with **Quit TM Publisher** at the bottom of the sidebar
     (closing the window only hid it), and remove old shortcuts or pins to it. While the new scheduler finds an old version on the
     same data folder it does not start and claims no post; on Windows an old version started later ends at once. On Linux an
     old version started after the new scheduler sends the posts itself until you quit it (the new one waits), and a post the new
     one was sending at that moment can come back as interrupted. So quit the old version first.
   - **From 0.1.3 on:** close the window and stop the scheduler (Settings → **Stop**, or the tray icon → **Quit**).
3. Windows: unpack the new zip and replace the files of the `TM Publisher` folder (same place, so autostart keeps working). Coming
   from 0.1.0–0.1.2, which was a single `TMPublisher-<version>-win-x64.exe`: put the new folder where you like, start
   `TMPublisher.exe` there, and delete the old exe; the autostart entry is moved to the new scheduler when the new window starts.
   Linux: unpack the new archive over the old folder.
4. Start the window. Your data stays where it was.

## Uninstall

1. In **Settings**, turn off **Start the background scheduler with my computer** (this removes the Windows autostart entry or
   disables the Linux one), then **Stop** the scheduler there.
2. Close the window.
3. Delete the `TM Publisher` folder (Windows) or the `tm-publisher` folder (Linux), and on Linux
   `~/.config/autostart/tm-publisher.desktop`.
4. To remove your data too, delete the data folder below. This cannot be undone; make a backup first if you may need it.
   On Linux with the keyring, also delete the "TM Publisher database key" entry in your keyring app (Seahorse, KWallet...).

## Where your data is

| | Folder |
|---|---|
| Windows | `%LOCALAPPDATA%\TMPublisher` |
| Linux | `~/.local/share/tm-publisher` (or `$XDG_DATA_HOME/tm-publisher`) |

Inside: `publisher.db` (the encrypted database), `key.json` (the database key, sealed by DPAPI, the keyring or your master
password), `preferences.json` (only the interface language), `logs/` (one file per day and program: `app-yyyy-MM-dd.log` for
the window, `scheduler-…` for the scheduler, `notify-…` for notifications; kept 7 days; never contain tokens or passwords),
`reports/` (only the logs zips you make with "Report a problem"), `scheduler-instance.lock` / `window-instance.lock` (one scheduler and one window per data folder),
`scheduler-paused.txt` (only while scheduling is paused) and `locks/` (token refreshes of one account never run twice at once).
Media files stay where you keep them; only their paths are stored.

Another folder can be used with `--data-dir <folder>` or the `TMPUBLISHER_DATA_DIR` environment variable; the autostart entry keeps
that folder. The window program unpacks its native libraries to the system's temporary folder on first start (standard .NET
single-file behavior).

The window draws with the processor, not the graphics card. If drawing feels slow on a very large screen, start it with `--gpu`
(or set the environment variable `TMPUBLISHER_GPU=1`) to draw with the graphics card.

## Reporting a problem

The **Report a problem** button is at the bottom of the side menu. Nothing is sent automatically:

- **Prepare logs** makes `tm-publisher-logs-<date>-<time>.zip` in the data folder's `reports/` folder and shows it: the log files
  of the last 7 days and `system.txt` (version, operating system, architecture, .NET, interface language, drawing mode, which
  helper programs are there). Your home folder, e-mail addresses and tokens are masked; settings, keys, the database, account
  names and post texts are never in it. The newest 5 zips are kept.
- **Report on GitHub** opens the bug form in your browser with the version and system filled in; describe what happened and drag
  the zip into it.
- **Copy details** copies the same `system.txt` text.

The logs are in the data folder's `logs/` folder, one file per day and program, Information level and above; only the last 7
days (today included) are kept, older files are deleted at start and at each day change, and a file stops growing for the day
at 20 MB.

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
dotnet run --project src/TMPublisher.Desktop     # the window; the scheduler is src/TMPublisher.Background
```

Release files, from a clean working tree only (the programs carry the commit they were built from): `pwsh tools/publish.ps1`
on Windows (win-x64: the window trimmed single file, the scheduler with Native AOT, which needs Visual Studio's C++ tools) and
`tools/publish.sh` on Linux (the scheduler is Native AOT for the machine's own architecture, built with clang or gcc, and a
trimmed ReadyToRun single file for the other one). Packages and `SHA256SUMS.txt` go to `artifacts/release` (another folder: `-Out <folder>` /
`TMPUBLISHER_OUT=<folder>`). Platform guides in `docs/platforms` are generated from the app's own texts with
`python tools/gen-platform-docs.py`.

## License

MIT, see [LICENSE](LICENSE). Third-party material: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
