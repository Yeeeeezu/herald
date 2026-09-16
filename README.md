# herald

send windows toast notifications from the command line.

uses `Windows.UI.Notifications` WinRT through a powershell subprocess. no app manifest, no package identity, no nuget required. falls back to `msg.exe` if WinRT is unavailable.

```
herald <message> [flags]
```

---

## examples

```sh
herald "build finished"
herald "tests passed" -t "ci"
herald "deploy done" -t "prod"

# use in scripts
dotnet build && herald "build ok" -t "ci" || herald "build failed" -t "ci"
```

## flags

| flag | description |
|------|-------------|
| `-t, --title <text>` | notification title (default: `herald`) |
| `-d, --duration <sec>` | display duration in seconds (default: 5) |

## build

```
dotnet build -c Release
```

.NET 8+, windows only.

## testing

built clean. WinRT notification XML structure and powershell call verified against documentation. fallback to `msg.exe` confirmed in code.

**not live-tested:** didn't fire a real toast. the powershell WinRT approach is documented and widely used, but wasn't exercised against a real desktop session.

## license

MIT
