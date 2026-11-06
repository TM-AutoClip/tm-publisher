# Third-party notices

TM Publisher is MIT licensed. It includes or uses the following third-party material.

## Included in this repository

| Component | License | Where |
|---|---|---|
| twitter-text top-level domain list (`conformance/tld_lib.yml`) | Apache-2.0 | `src/TMPublisher.Core/Text/Concrete/XDomains.cs` |
| Manrope font | SIL Open Font License 1.1 | `src/TMPublisher.Desktop/Assets/Fonts`, license text in `src/TMPublisher.Desktop/Assets/Licenses/font-manrope.txt` |
| Space Grotesk font | SIL Open Font License 1.1 | `src/TMPublisher.Desktop/Assets/Fonts`, license text in `src/TMPublisher.Desktop/Assets/Licenses/font-space-grotesk.txt` |

The font license texts are embedded in the program and shipped as files in the Linux archive (`licenses/`).

## Libraries bundled into the program

The release files are self-contained: the one program file carries the .NET runtime and these libraries (the native ones inside the
file too). Each keeps its own license.

| Library | License |
|---|---|
| .NET runtime and Microsoft.Extensions.* | MIT |
| Avalonia (UI) with SkiaSharp and HarfBuzzSharp | MIT |
| CommunityToolkit.Mvvm | MIT |
| Microsoft.Data.Sqlite | MIT |
| SQLite3 Multiple Ciphers (SQLite3MC.PCLRaw.bundle) and SQLitePCLRaw | MIT |
| SQLite | Public domain |
| Konscious.Security.Cryptography.Argon2 | MIT |
| System.Security.Cryptography.ProtectedData | MIT |
| Tmds.DBus.Protocol (used by Avalonia on Linux) | MIT |

Exact versions are in `Directory.Packages.props` (Tmds.DBus.Protocol comes with Avalonia).
