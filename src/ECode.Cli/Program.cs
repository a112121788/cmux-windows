using System.Text.Json;
using ECode.Core.Config;
using ECode.Core.IPC;
using ECode.Core.IPC.V2;
using ECode.Core.Services;

namespace ECode.Cli;

/// <summary>
/// ecode 命令行工具
/// 通过命名管道与正在运行的 ecode 应用通信。
///
/// 用法：
///   ecode notify --title "Title" --body "Body"
///   ecode workspace list
///   ecode workspace create --name "My Project"
///   ecode workspace select --index 0
///   ecode surface create
///   ecode split right
///   ecode split down
///   ecode status
/// </summary>
public static class Program
{
    private static CliGlobalOptions _globalOptions = CliGlobalOptions.DefaultHuman;

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        // 兼容期：若用户在 CompatAcceptLegacyCliCommand=true 时仍然以
        // `cmux ...` 形式调用，把首参重写为 `ecode` 之后再走分派。
        if (CompatibilityOptions.ShouldAcceptLegacyCliCommand()
            && string.Equals(args[0], "cmux", StringComparison.Ordinal))
        {
            args = new[] { "ecode" }.Concat(args.Skip(1)).ToArray();
        }

        if (!CliGlobalOptions.TryExtract(args, out _globalOptions, out args, out var globalError))
            return Error(globalError);

        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();

        try
        {
            return command switch
            {
                "notify" => await HandleNotify(args[1..]),
                "notification" or "notifications" => await HandleNotification(args[1..]),
                "window" => await HandleWindow(args[1..]),
                "workspace" => await HandleWorkspace(args[1..]),
                "surface" => await HandleSurface(args[1..]),
                "pane" => await HandlePane(args[1..]),
                "browser" => await HandleBrowser(args[1..]),
                "split" => await HandleSplit(args[1..]),
                "restore-session" => await HandleRestoreSession(args[1..]),
                "config" => await HandleConfig(args[1..]),
                "reload-config" => await HandleReloadConfig(),
                "status" => await HandleStatus(),
                "health" => await HandleHealth(),
                "help" or "--help" or "-h" => PrintHelp(),
                "version" or "--version" or "-v" => PrintVersion(),
                _ => Error($"Unknown command: {command}"),
            };
        }
        catch (TimeoutException)
        {
            Console.Error.WriteLine("Error: Could not connect to ecode. Is it running?");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> HandleNotify(string[] args)
    {
        var parsed = ParseArgs(args);
        var title = parsed.GetValueOrDefault("title", parsed.GetValueOrDefault("_arg0", "Terminal"));
        var body = parsed.GetValueOrDefault("body", parsed.GetValueOrDefault("_arg1", ""));
        var subtitle = parsed.GetValueOrDefault("subtitle");

        var cmdArgs = new Dictionary<string, string>
        {
            ["title"] = title,
            ["body"] = body,
        };
        if (subtitle != null) cmdArgs["subtitle"] = subtitle;

        var response = await NamedPipeClient.SendCommand("NOTIFY", cmdArgs);
        Console.WriteLine(response);
        return 0;
    }

    private static async Task<int> HandleNotification(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ecode notification <list|read|unread|jump-latest|clear>");
            return 1;
        }

        var subcommand = args[0].ToLowerInvariant();
        var parsed = ParseArgs(args[1..]);
        NormalizeNotificationArgs(subcommand, parsed);

        var method = subcommand switch
        {
            "list" or "ls" => "notification.list",
            "read" => "notification.read",
            "unread" => "notification.unread",
            "jump-latest" or "jump" => "notification.jump-latest",
            "clear" => "notification.clear",
            _ => "",
        };

        return string.IsNullOrEmpty(method)
            ? Error($"Unknown notification command: {subcommand}")
            : await SendV2AndPrint(method, parsed);
    }

    private static async Task<int> HandleWindow(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ecode window <list|current|focus|create|close>");
            return 1;
        }

        var subcommand = args[0].ToLowerInvariant();
        var parsed = ParseArgs(args[1..]);
        NormalizeWindowArgs(subcommand, parsed);

        var method = subcommand switch
        {
            "list" or "ls" => "window.list",
            "current" => "window.current",
            "focus" => "window.focus",
            "create" or "new" => "window.create",
            "close" => "window.close",
            _ => "",
        };

        return string.IsNullOrEmpty(method)
            ? Error($"Unknown window command: {subcommand}")
            : await SendV2AndPrint(method, parsed);
    }

    private static async Task<int> HandleWorkspace(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ecode workspace <list|create|select|close|rename|reorder>");
            return 1;
        }

        var subcommand = args[0].ToLowerInvariant();
        var parsed = ParseArgs(args[1..]);
        NormalizeWorkspaceArgs(subcommand, parsed);

        return subcommand switch
        {
            "list" or "ls" => await SendV2AndPrint("workspace.list", parsed),
            "create" or "new" => await SendV2AndPrint("workspace.create", parsed),
            "select" => await SendV2AndPrint("workspace.select", parsed),
            "close" => await SendV2AndPrint("workspace.close", parsed),
            "rename" => await SendV2AndPrint("workspace.rename", parsed),
            "reorder" => await SendV2AndPrint("workspace.reorder", parsed),
            "next" => await SendAndPrint("WORKSPACE.NEXT"),
            "previous" or "prev" => await SendAndPrint("WORKSPACE.PREVIOUS"),
            _ => Error($"Unknown workspace command: {subcommand}"),
        };
    }

    private static async Task<int> HandleSurface(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ecode surface <create|next|previous|resume>");
            return 1;
        }

        var subcommand = args[0].ToLowerInvariant();

        return subcommand switch
        {
            "create" or "new" => await SendAndPrint("SURFACE.CREATE"),
            "move" => await HandleSurfaceMove(args[1..]),
            "reorder" => await HandleSurfaceReorder(args[1..]),
            "next" => await SendAndPrint("SURFACE.NEXT"),
            "previous" or "prev" => await SendAndPrint("SURFACE.PREVIOUS"),
            "resume" => await HandleSurfaceResume(args[1..]),
            _ => Error($"Unknown surface command: {subcommand}"),
        };
    }

    private static async Task<int> HandleSurfaceMove(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeSurfaceMoveArgs(parsed);
        return await SendV2AndPrint("surface.move", parsed);
    }

    private static async Task<int> HandleSurfaceReorder(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeSurfaceReorderArgs(parsed);
        return await SendV2AndPrint("surface.reorder", parsed);
    }

    private static async Task<int> HandlePane(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ecode pane <list|focus|write|read|split|close|resize|swap|zoom>");
            return 1;
        }

        var subcommand = args[0].ToLowerInvariant();
        var parsed = ParseArgs(args[1..]);
        NormalizePaneArgs(subcommand, parsed);

        var method = subcommand switch
        {
            "list" or "ls" => "pane.list",
            "focus" => "pane.focus",
            "write" => "pane.write",
            "read" => "pane.read",
            "split" => "pane.split",
            "close" => "pane.close",
            "resize" => "pane.resize",
            "swap" => "pane.swap",
            "zoom" => "pane.zoom",
            _ => "",
        };

        return string.IsNullOrEmpty(method)
            ? Error($"Unknown pane command: {subcommand}")
            : await SendV2AndPrint(method, parsed);
    }

    private static async Task<int> HandleSurfaceResume(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ecode surface resume <show|set|clear>");
            return 1;
        }

        var subcommand = args[0].ToLowerInvariant();
        var parsed = ParseArgs(args[1..]);
        NormalizeResumeSelectorAliases(parsed);

        return subcommand switch
        {
            "show" or "ls" or "list" => await SendAndPrint("SURFACE.RESUME.SHOW", parsed),
            "set" => await SendAndPrint("SURFACE.RESUME.SET", parsed),
            "clear" or "rm" or "remove" => await SendAndPrint("SURFACE.RESUME.CLEAR", parsed),
            _ => Error($"Unknown surface resume command: {subcommand}"),
        };
    }

    private static async Task<int> HandleSplit(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ecode split <right|down>");
            return 1;
        }

        var direction = args[0].ToLowerInvariant();

        return direction switch
        {
            "right" or "vertical" or "v" => await SendAndPrint("SPLIT.RIGHT"),
            "down" or "horizontal" or "h" => await SendAndPrint("SPLIT.DOWN"),
            _ => Error($"Unknown split direction: {direction}"),
        };
    }

    private static async Task<int> HandleBrowser(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ecode browser <open|new|open-split> <url>");
            return 1;
        }

        var subcommand = args[0].ToLowerInvariant();
        var parsed = ParseArgs(args[1..]);
        NormalizeBrowserArgs(subcommand, parsed);

        return BrowserScriptingCliCommands.TryResolve(subcommand, out var pipeCommand)
            ? await SendAndPrint(pipeCommand, parsed)
            : Error($"Unknown browser command: {subcommand}");
    }

    private static async Task<int> HandleStatus()
    {
        return await SendAndPrint("STATUS");
    }

    private static async Task<int> HandleHealth()
    {
        return await SendV2AndPrint("health");
    }

    private static async Task<int> HandleRestoreSession(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeResumeSelectorAliases(parsed);
        return await SendAndPrint("SESSION.RESTORE", parsed);
    }

    private static async Task<int> HandleConfig(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ecode config <reload|diagnostics>");
            return 1;
        }

        var subcommand = args[0].ToLowerInvariant();
        var method = subcommand switch
        {
            "reload" => "config.reload",
            "diagnostics" or "diag" => "config.diagnostics",
            _ => "",
        };

        return string.IsNullOrEmpty(method)
            ? Error($"Unknown config command: {subcommand}")
            : await SendV2AndPrint(method, ParseArgs(args[1..]));
    }

    private static async Task<int> HandleReloadConfig()
    {
        return await SendAndPrint("CONFIG.RELOAD");
    }

    private static async Task<int> SendAndPrint(string command, Dictionary<string, string>? args = null)
    {
        var response = await NamedPipeClient.SendCommand(command, MergeGlobalArgs(args));

        if (_globalOptions.Json)
        {
            Console.WriteLine(response);
            return 0;
        }

        // 美化输出 JSON
        try
        {
            using var doc = JsonDocument.Parse(response);
            var pretty = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(pretty);
        }
        catch
        {
            Console.WriteLine(response);
        }

        return 0;
    }

    private static async Task<int> SendV2AndPrint(string method, Dictionary<string, string>? args = null)
    {
        var request = new V2Request
        {
            Id = JsonSerializer.SerializeToElement(Guid.NewGuid().ToString()),
            Method = method,
            Params = JsonSerializer.SerializeToElement(MergeGlobalArgs(args)),
        };
        var response = await NamedPipeClient.SendV2Request(request);

        if (_globalOptions.Json)
        {
            Console.WriteLine(response);
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(response);
            var pretty = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(pretty);
        }
        catch
        {
            Console.WriteLine(response);
        }

        return 0;
    }

    private static Dictionary<string, string> MergeGlobalArgs(Dictionary<string, string>? args)
    {
        var merged = _globalOptions.ToPipeArgs();
        if (args != null)
        {
            foreach (var (key, value) in args)
                merged[key] = value;
        }

        return merged;
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result = new Dictionary<string, string>();
        int positional = 0;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith("--"))
            {
                var key = arg[2..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    result[key] = args[i + 1];
                    i++;
                }
                else
                {
                    result[key] = "true";
                }
            }
            else if (arg.StartsWith('-') && arg.Length == 2)
            {
                var key = arg[1..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    result[key] = args[i + 1];
                    i++;
                }
                else
                {
                    result[key] = "true";
                }
            }
            else
            {
                result[$"_arg{positional}"] = arg;
                positional++;
            }
        }

        return result;
    }

    private static void NormalizeResumeSelectorAliases(Dictionary<string, string> args)
    {
        CopyAlias(args, "workspace", "workspaceName");
        CopyAlias(args, "surface", "surfaceName");
        CopyAlias(args, "pane", "paneName");
        CopyAlias(args, "cwd", "workingDirectory");
    }

    private static void NormalizeBrowserArgs(string subcommand, Dictionary<string, string> args)
    {
        if (subcommand is "open" or "new" or "open-split" or "split")
            CopyAlias(args, "_arg0", "url");
        else if (subcommand is "eval")
            CopyAlias(args, "_arg0", "script");
        else if (subcommand is "fill")
        {
            CopyAlias(args, "_arg0", "testid");
            CopyAlias(args, "_arg1", "value");
        }
        else if (subcommand is "click" or "hover" or "press")
            CopyAlias(args, "_arg0", "testid");

        CopyAlias(args, "surface-ref", "surfaceRef");
        CopyAlias(args, "surface-id", "surfaceId");
        CopyAlias(args, "test-id", "testid");
        CopyAlias(args, "testId", "testid");
        CopyAlias(args, "workspace", "workspaceName");
        CopyAlias(args, "surface", "surfaceName");
    }

    private static void NormalizeWorkspaceArgs(string subcommand, Dictionary<string, string> args)
    {
        if (subcommand is "select" or "close")
            CopyAlias(args, "_arg0", "target");
        else if (subcommand is "create" or "new")
            CopyAlias(args, "_arg0", "name");
        else if (subcommand is "rename")
        {
            CopyAlias(args, "_arg0", "target");
            CopyAlias(args, "_arg1", "name");
        }
        else if (subcommand is "reorder")
        {
            CopyAlias(args, "_arg0", "order");
        }

        CopyAlias(args, "workspace-ref", "target");
    }

    private static void NormalizeNotificationArgs(string subcommand, Dictionary<string, string> args)
    {
        if (subcommand is "read" or "unread")
            CopyAlias(args, "_arg0", "target");
        else if (subcommand is "list" or "ls")
            CopyAlias(args, "unread", "unreadOnly");

        CopyAlias(args, "notification-id", "target");
        CopyAlias(args, "workspace", "workspaceId");
        CopyAlias(args, "surface", "surfaceId");
        CopyAlias(args, "pane", "paneId");
    }

    private static void NormalizeWindowArgs(string subcommand, Dictionary<string, string> args)
    {
        if (subcommand is "focus" or "close")
            CopyAlias(args, "_arg0", "target");
        else if (subcommand is "create" or "new")
            CopyAlias(args, "_arg0", "title");
    }

    private static void NormalizeSurfaceMoveArgs(Dictionary<string, string> args)
    {
        CopyAlias(args, "_arg0", "target");
        CopyAlias(args, "_arg1", "targetIndex");
        CopyAlias(args, "to", "targetIndex");
        CopyAlias(args, "workspace-ref", "workspace");
        CopyAlias(args, "surface-ref", "target");
    }

    private static void NormalizeSurfaceReorderArgs(Dictionary<string, string> args)
    {
        CopyAlias(args, "_arg0", "order");
        CopyAlias(args, "workspace-ref", "workspace");
    }

    private static void NormalizePaneArgs(string subcommand, Dictionary<string, string> args)
    {
        if (subcommand is "focus" or "close")
            CopyAlias(args, "_arg0", "target");
        else if (subcommand is "write")
        {
            CopyAlias(args, "_arg0", "text");
            CopyAlias(args, "pane-ref", "target");
        }
        else if (subcommand is "read")
        {
            CopyAlias(args, "_arg0", "target");
        }
        else if (subcommand is "split")
        {
            CopyAlias(args, "_arg0", "direction");
            CopyAlias(args, "pane-ref", "target");
        }
        else if (subcommand is "resize")
        {
            CopyAlias(args, "_arg0", "target");
            CopyAlias(args, "_arg1", "delta");
        }
        else if (subcommand is "swap")
        {
            CopyAlias(args, "_arg0", "target");
            CopyAlias(args, "_arg1", "other");
        }
        else if (subcommand is "zoom")
        {
            CopyAlias(args, "_arg0", "value");
        }

        CopyAlias(args, "workspace-ref", "workspace");
        CopyAlias(args, "surface-ref", "surface");
        CopyAlias(args, "pane-ref", "target");
    }

    private static void CopyAlias(Dictionary<string, string> args, string source, string target)
    {
        if (args.ContainsKey(target))
            return;

        if (args.TryGetValue(source, out var value))
            args[target] = value;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            ecode - Terminal multiplexer (Windows)

            Usage:
              ecode [--json] [--id-format refs|uuids|both] <command> [options]

            Global options:
              --id-format <mode>   refs | uuids | both; default refs, or both with --json
              --json               Print raw JSON and default id-format to both

            Commands:
              notify                Send a notification
                --title <text>      Notification title (default: "Terminal")
                --body <text>       Notification body
                --subtitle <text>   Notification subtitle

              notification          Manage notifications through ecode.v2
                list                List notifications
                  --unread true     Only show unread notifications
                read <id>           Mark a notification as read
                  --all true        Mark all notifications as read
                unread <id>         Mark a notification as unread
                jump-latest         Jump to latest unread notification
                clear               Clear all notifications

              window                Manage app windows through ecode.v2
                list                List all open windows
                current             Show the current window
                focus <ref|id>      Focus a window, e.g. window:1
                create [title]      Create and show a new window
                close <ref|id>      Close a window

              workspace             Manage projects
                list                List all projects
                create              Create a new project
                  --name <text>     Project name
                select              Select a project
                  <ref|id|name>     Project ref, ID, or name
                close [ref|id|name] Close selected or target project
                rename <ref|id> <name>
                                    Rename a project
                reorder <order>     Reorder projects, e.g. "workspace:2,workspace:1"
                  --target <value>  Project ref, ID, or name
                next                Switch to next project
                previous            Switch to previous project

              surface               Manage surfaces (tabs within project)
                create              Create a new surface
                move <ref|id> <n>   Move a surface to index n within its workspace
                reorder <order>     Reorder surfaces, e.g. "surface:2,surface:1"
                next                Switch to next surface
                previous            Switch to previous surface
                resume show         Show resume binding for focused/selected pane
                  --all             Show all bindings in the selected surface
                resume set          Save resume command for focused/selected pane
                  --shell <cmd>     Command to run, or pass it as first positional arg
                  --kind <kind>     tmux | custom (default: custom)
                  --checkpoint <id> Optional checkpoint/session label
                  --cwd <path>      Working directory override
                  --trusted <bool>  Mark binding trusted for future restore
                resume clear        Clear binding by --id or focused/selected pane

              pane                  Manage panes through ecode.v2
                list                List panes in the selected surface
                focus <ref|id>      Focus a pane, e.g. pane:1
                write <text>        Write text to a pane
                  --submit true     Send Enter after writing
                read [ref|id]       Read pane tail text
                  --lines <n>       Tail line count
                split [direction]   Split focused pane; right/down
                close [ref|id]      Close a pane
                resize <ref|id> <d> Resize nearest split ratio by delta
                swap <a> <b>        Swap two panes
                zoom [true|false]   Set or toggle surface zoom

              split                 Split the focused pane
                right               Split vertically (left/right)
                down                Split horizontally (top/bottom)

              browser               Open browser surfaces
                open <url>          Open URL in current browser surface or a new one
                new <url>           Create a new browser surface
                open-split <url>    v1 compatibility entry; opens a browser surface
                  --direction <dir> right | down (reserved for mixed-pane support)
                snapshot            Print accessibility snapshot for a browser surface
                click               Click by --testid, --text, or --role [--name]
                fill                Fill by --testid, --text, or --role [--value <text>]
                eval <script>       Execute JavaScript in the browser surface
                screenshot          Capture PNG screenshot as base64 JSON
                  --surfaceRef <ref> Browser surface ref returned by open/new

              reload-config         Reload ecode.json commands/actions

              config                Manage ecode.json through ecode.v2
                reload              Reload ecode.json commands/actions/layout
                diagnostics         Show diagnostics from last or fresh reload

              restore-session       Refresh resume bindings and focus first recoverable pane
                --all               Scan all workspaces/surfaces
                --trusted           Also run trusted bindings immediately
                --workspace <name>  Select workspace by name
                --surface <name>    Select surface by name

              status                Show ecode status
              health                Show ecode.v2 health summary

            Keyboard Shortcuts (in the app):
              Ctrl+N                New project
              Ctrl+1-8              Jump to project 1-8
              Ctrl+9                Jump to last project
              Ctrl+Shift+W          Close project
              Ctrl+B                Toggle sidebar
              Ctrl+T                New surface (tab)
              Ctrl+W                Close surface
              Ctrl+D                Split right
              Ctrl+Shift+D          Split down
              Ctrl+Alt+Arrow        Focus pane directionally
              Ctrl+I                Toggle notification panel
              Ctrl+Shift+U          Jump to latest unread
              Ctrl+Shift+O          Restore session bindings
              Ctrl+Shift+,          Reload ecode.json
            """);
        return 0;
    }

    private static int PrintVersion()
    {
        var version = VersionService.GetInformationalVersion(typeof(Program).Assembly);
        Console.WriteLine($"ecode {version} (Windows)");
        return 0;
    }

    private static int Error(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
        return 1;
    }
}
