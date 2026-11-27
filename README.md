# PlaytimeLock

A [Playnite](https://playnite.link) extension that stops library updates from resetting your playtime.

A store reports 0 (or less) playtime for games you played offline. You can fix the value by hand in Playnite, but every library update overwrites it again. After you lock a game, its playtime can no longer go down.

UI languages: English, Türkçe.

## Usage

- Right-click a game → **Lock playtime...** → enter the total playtime to keep (`12`, `12.5`, `12:30`, `45m`). The current value is pre-filled.
- Right-click a locked game → **Unlock playtime**.
- If you select several games, each is locked at its current playtime.
- Main menu → Extensions → PlaytimeLock → **List locked games** shows everything you locked.

## How it works

- If a library update lowers the playtime or play count of a locked game, the locked value is restored right away, silently.
- Increases are accepted and remembered. That covers time Playnite tracks while you play and higher values from the store.
- To change a locked playtime, unlock the game and lock it again with the new value.
- Optionally, locked games get a `Playtime Locked` tag so you can filter for them (setting).

## Building

Requires the [.NET SDK](https://dotnet.microsoft.com/download). Visual Studio is not needed.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Install
```

The command creates `dist\*.pext` and opens it, so Playnite installs it. Pushing a tag such as `v1.0` makes GitHub Actions build the package and attach it to a release.

## License

MIT
