using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using DataFactory.MCP.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace DataFactory.MCP.Services;

/// <summary>
/// User notification service that shows native OS toast/banner notifications.
/// Cross-platform: Windows (WPF), macOS (osascript), Linux (notify-send).
/// Best for stdio mode where the server runs locally on the user's machine.
/// </summary>
public class SystemToastNotificationService : IUserNotificationService
{
    private readonly ILogger<SystemToastNotificationService> _logger;
    private readonly string _xamlTemplate;
    private readonly string _psScriptTemplate;

    public SystemToastNotificationService(ILogger<SystemToastNotificationService> logger)
    {
        _logger = logger;
        _xamlTemplate = LoadEmbeddedResource("ToastNotification.xaml");
        _psScriptTemplate = LoadEmbeddedResource("ToastNotification.ps1");
    }

    public Task NotifyAsync(string title, string message, NotificationLevel level = NotificationLevel.Info)
    {
        try
        {
            Show(title, message, level);
            _logger.LogDebug("System notification shown: {Title}", title);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show system notification: {Title}", title);
        }

        return Task.CompletedTask;
    }

    private void Show(string title, string message, NotificationLevel level)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Run("osascript", $"-e \"display notification \\\"{EscapeAppleScript(message)}\\\" with title \\\"{EscapeAppleScript(title)}\\\"\"");
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Run("notify-send", $"{Quote(title)} {Quote(message)}");
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ShowWindowsToast(title, message, level);
            return;
        }

        _logger.LogDebug("No notification mechanism available for this platform");
    }

    private void ShowWindowsToast(string title, string message, NotificationLevel level)
    {
        var icon = level switch
        {
            NotificationLevel.Success => "✅",
            NotificationLevel.Error => "❌",
            NotificationLevel.Warning => "⚠️",
            _ => "ℹ️"
        };

        var borderColor = level switch
        {
            NotificationLevel.Success => "#28A745",
            NotificationLevel.Error => "#DC3545",
            NotificationLevel.Warning => "#FFC107",
            _ => "#007ACC"
        };

        var safeTitle = EscapeXml($"{icon} {title}");
        var safeMessage = EscapeXml(message);

        var xamlContent = _xamlTemplate
            .Replace("{{BorderColor}}", borderColor)
            .Replace("{{Title}}", safeTitle)
            .Replace("{{Message}}", safeMessage);

        var psScript = _psScriptTemplate.Replace("{{XamlContent}}", xamlContent);

        try
        {
            var scriptPath = Path.Combine(Path.GetTempPath(), $"mcp-toast-{Guid.NewGuid():N}.ps1");
            File.WriteAllText(scriptPath, psScript, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            _ = Task.Delay(10000).ContinueWith(_ =>
            {
                try { File.Delete(scriptPath); } catch { }
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to show Windows toast notification");
        }
    }

    private static string LoadEmbeddedResource(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"DataFactory.MCP.Core.Resources.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void Run(string file, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to run notification process: {File}", file);
        }
    }

    private static string EscapeXml(string s) =>
        s.Replace("&", "&amp;")
         .Replace("<", "&lt;")
         .Replace(">", "&gt;")
         .Replace("\"", "&quot;")
         .Replace("'", "&apos;");

    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string EscapeAppleScript(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
