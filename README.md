# LocoSwap

Utility to swap the rolling stock of DTG's Train Simulator Classic scenarios.

Pick a route and a scenario, see every consist and every vehicle it uses, and
replace vehicles (individually or in bulk) with stock you actually own. Vehicles
that are missing from your install are flagged, and LocoSwap points you at the
closest matching asset folder.

This is a modernised fork of [flicard/LocoSwap](https://github.com/flicard/LocoSwap).
The upstream project had not been touched in several years; this fork ports it to
current .NET and adds a number of features and fixes (see [CHANGELOG.md](CHANGELOG.md)).

## Highlights

* Runs on **.NET 10** (upstream was .NET Framework 4.8)
* Settings stored as `%AppData%\LocoSwap\settings.json`, imported automatically on first run
* Reads and edits scenarios packed inside `.ap` archives
* Fast startup and route scanning on large installs; scenario consist checks run in parallel and are cached
* Vehicle lookup scan cached on disk between sessions
* Remembers window position and last route; crash dialog with log location instead of a silent exit
* 8 UI languages: English, French, German, Italian, Dutch, Russian, Spanish, Polish

## Download

Grab `LocoSwap.exe` from the [latest release](https://github.com/Samueleonard/LocoSwap/releases/latest).
It is a self-contained single file — no .NET runtime install required. Windows x64 only.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build LocoSwap.sln -c Release
dotnet test LocoSwap.sln -c Release
```

To produce the self-contained exe:

```
dotnet publish LocoSwap/LocoSwap.csproj -c Release -r win-x64 --self-contained ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## Manual

The user manual source is [LocoSwap_manual.docx](LocoSwap_manual.docx). Export it to
`LocoSwap_manual.pdf` in the repo root before publishing and the build will ship it
next to the exe; otherwise the in-app Help button falls back to this repository.

## Credits

App icon: [Architecture et ville icônes créées par Freepik - Flaticon](https://www.flaticon.com/fr/icones-gratuites/architecture-et-ville)
