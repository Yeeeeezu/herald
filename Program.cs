using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Herald;

// send windows toast notifications from the command line.
// uses powershell's BurntToast-free approach via Windows.UI.Notifications COM interop
// through a powershell subprocess — no nuget, no app manifest required.

static class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0) { Help(); return; }

        string title = "herald";
        string body = "";
        string? icon = null;
        int duration = 5;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-t" or "--title":
                    if (i + 1 < args.Length) title = args[++i];
                    break;
                case "-i" or "--icon":
                    if (i + 1 < args.Length) icon = args[++i];
                    break;
                case "-d" or "--duration":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int sec)) { duration = sec; i++; }
                    break;
                default:
                    if (!args[i].StartsWith('-'))
                        body = args[i];
                    break;
            }
        }

        if (body.Length == 0) { Err("message body required"); return; }

        Send(title, body, icon, duration);
    }

    static void Send(string title, string body, string? icon, int duration)
    {
        // build a powershell one-liner using Windows.UI.Notifications
        // this works on windows 10+ without any manifest or package identity
        string escaped_title = title.Replace("'", "''").Replace("\"", "`\"");
        string escaped_body  = body.Replace("'", "''").Replace("\"", "`\"");

        string ps = $$"""
            $template = [Windows.UI.Notifications.ToastTemplateType, Windows.UI.Notifications, ContentType = WindowsRuntime]::ToastText02
            $xml = [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]::GetTemplateContent($template)
            $nodes = $xml.GetElementsByTagName('text')
            $nodes[0].AppendChild($xml.CreateTextNode('{{escaped_title}}')) | Out-Null
            $nodes[1].AppendChild($xml.CreateTextNode('{{escaped_body}}')) | Out-Null
            $toast = [Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType = WindowsRuntime]::new($xml)
            $notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('herald')
            $notifier.Show($toast)
            """;

        try
        {
            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -Command \"{ps}\"")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi)!;
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0 || err.Length > 0)
            {
                // powershell WinRT approach failed — fall back to a balloon via msg.exe
                FallbackBalloon(title, body);
            }
        }
        catch
        {
            FallbackBalloon(title, body);
        }
    }

    static void FallbackBalloon(string title, string body)
    {
        // msg.exe fallback for environments where WinRT isn't available
        try
        {
            var psi = new ProcessStartInfo("msg.exe", $"* /TIME:5 \"{title}: {body}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit();
        }
        catch (Exception ex)
        {
            Err($"notification failed: {ex.Message}");
        }
    }

    static void Err(string msg) => Console.Error.WriteLine($"  \x1b[31merror:\x1b[0m {msg}");

    static void Help() => Console.WriteLine("""

  herald — send windows toast notifications from the cli

  usage:
    herald <message> [flags]

  flags:
    -t, --title <text>       notification title (default: "herald")
    -d, --duration <sec>     display duration in seconds (default: 5)

  examples:
    herald "build finished"
    herald "tests passed" -t "ci"
    herald "deploy done" -t "prod"

""");
}
