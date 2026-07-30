using System;
using System.Reflection;
using Microsoft.Win32;

namespace ArcMapConditions.App.Services;

/// <summary>
/// Toggles "run at Windows logon" by managing a value under the per-user
/// HKCU Run key. Per-user (HKCU) needs no admin rights.
/// </summary>
public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ArcMapConditions";

    /// <summary>Full path to the running executable (works for single-file publishes).</summary>
    private static string ExePath =>
        Environment.ProcessPath ?? Assembly.GetEntryAssembly()?.Location ?? string.Empty;

    /// <summary>True only if the Run entry exists and points at this exe.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            if (key?.GetValue(ValueName) is not string value || string.IsNullOrWhiteSpace(value))
                return false;

            return string.Equals(Unquote(value), ExePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Adds or removes the Run entry. Returns the resulting state.</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (enabled)
            {
                string exe = ExePath;
                if (!string.IsNullOrEmpty(exe))
                    key.SetValue(ValueName, "\"" + exe + "\"", RegistryValueKind.String);
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // No permission / policy-restricted — fall through and report actual state.
        }

        return IsEnabled();
    }

    private static string Unquote(string value) => value.Trim().Trim('"');
}
