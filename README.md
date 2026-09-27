# ClearVault

A fully accessible, native Windows client for Dropbox, built from scratch for screen reader users.

## Why

The official Dropbox desktop app's tray icon opens a custom, Chromium-rendered panel that only partially responds to a screen reader, and its Preferences window is effectively silent — neither behaves like a normal Windows control. ClearVault replaces that with an ordinary WinForms app: real menus, real list views, real buttons. No embedded browser control anywhere in the app.

## Install

Download and run `ClearVaultSetup.exe` from the latest release. It's a normal per-user installer (no admin rights needed) with optional desktop-shortcut and start-at-login checkboxes, both off by default.

Alternatively, `ClearVault.exe` on its own is a self-contained, single-file executable — no .NET runtime install required, just run it directly.

## First-time setup

ClearVault doesn't ship with a shared Dropbox connection — each install creates its own free Dropbox API app, so your Dropbox activity never routes through anyone else's account. The app walks you through this on first launch (~2 minutes), and the same steps are always available afterward via **F1** inside the app, or in [`Help/help.html`](Help/help.html).

## Features

- **Multiple accounts** — sign in to more than one Dropbox account and switch between them from a single list; no sign-out/sign-in required.
- **File browsing** — navigate folders, open files with their default app, or download a copy.
- **File management** — new folder, upload, rename (`F2`), delete (`Delete`), move.
- **Search** — full-text search across your Dropbox.
- **Sharing** — get or reuse a share link and copy it to the clipboard in one step; optional direct-download link rewriting.
- **Background notifications** — ClearVault keeps running in the system tray and shows a short notification when something changes in your Dropbox (checked roughly every 90 seconds), including changes made while it wasn't running. The current status (Up to date, Checking for changes, Can't reach Dropbox...) is shown in the main window's status bar and the tray icon's tooltip and menu.
- **In-app help** — press `F1` anywhere in the app.

## Keyboard shortcuts

| Key | Where | Action |
|---|---|---|
| `F1` | Anywhere | Open help |
| `Enter` | File list | Open selected file/folder |
| `Backspace` | File list | Go up one folder |
| `F2` | File list | Rename selected item |
| `Delete` | File list | Delete selected item (confirms first) |

## Building from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build
dotnet run --project .
```

To produce a self-contained release build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

The installer is built with [Inno Setup](https://jrsoftware.org/isinfo.php) from `installer.iss`:

```powershell
ISCC.exe installer.iss
```

## Architecture notes

ClearVault does not reimplement Dropbox's file-sync engine — it's purely a client for the Dropbox Web API (browsing, sharing, search, account management). If you also use the official Dropbox desktop app for background file syncing, both can coexist; ClearVault doesn't touch local sync state.

Built with WinForms on .NET 9, deliberately not WPF or Electron — plain native controls have the most reliable out-of-the-box screen reader support, which is the entire point of this project.

## Not affiliated with Dropbox

ClearVault is an independent, unofficial client. It is not made or endorsed by Dropbox, Inc.
