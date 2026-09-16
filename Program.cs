using System.Diagnostics;
using System.Text;

namespace Herald;

// send windows toast notifications from the command line.
// uses WinRT through a powershell encoded-command invocation.
// no nuget, no app manifest, no package identity required.

static class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0) { Help(); return; }

        string title = "herald";
        string body = "";
        int duration = 5;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-t" or "--title":
                    if (i + 1 < args.Length) title = args[++i];
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

        Send(title, body, duration);
    }

    static void Send(string title, string body, int duration)
    {
        // Build the PS script and pass it as -EncodedCommand so that user-supplied
        // title/body strings are embedded as string literals, not interpolated
        // into the shell. This avoids any injection through special PS characters.
        string script = $"""
            $t = '{EscapeForSingleQuote(title)}'
            $b = '{EscapeForSingleQuote(body)}'
            $template = [Windows.UI.Notifications.ToastTemplateType, Windows.UI.Notifications, ContentType = WindowsRuntime]::ToastText02
            $xml = [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]::GetTemplateContent($template)
            $nodes = $xml.GetElementsByTagName('text')
            $nodes[0].AppendChild($xml.CreateTextNode($t)) | Out-Null
            $nodes[1].AppendChild($xml.CreateTextNode($b)) | Out-Null
            $toast = [Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType = WindowsRuntime]::new($xml)
            $notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('herald')
            $notifier.Show($toast)
            """;

        // -EncodedCommand takes Base64 UTF-16LE — no shell escaping needed
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -NonInteractive -EncodedCommand {encoded}")
            {
                UseShellExecute     = false,
                RedirectStandardError = true,
                CreateNoWindow      = true,
            };
            using var p = Process.Start(psi)!;
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
                FallbackMsg(title, body);
        }
        catch
        {
            FallbackMsg(title, body);
        }
    }

    // single-quote escape for PS string literals: ' → ''
    static string EscapeForSingleQuote(string s) => s.Replace("'", "''");

    static void FallbackMsg(string title, string body)
    {
        try
        {
            var psi = new ProcessStartInfo("msg.exe", $"* /TIME:5 \"{title}: {body}\"")
            {
                UseShellExecute = false,
                CreateNoWindow  = true,
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
    dotnet build && herald "build ok" -t ci || herald "build failed" -t ci

""");
}
