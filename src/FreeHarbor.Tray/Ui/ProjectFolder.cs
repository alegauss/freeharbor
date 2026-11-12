using System.Diagnostics;
using System.IO;

namespace FreeHarbor.Tray.Ui;

/// <summary>
/// Copy a project's compose file path and open its folder: the two verbs beside the path in Edit
/// details (DD333) and on the project's page, written once so the two cannot do different things.
/// </summary>
internal static class ProjectFolder
{
    /// <summary>Put the compose files compose recorded on the clipboard, as it recorded them.</summary>
    /// <param name="row">The project or service.</param>
    public static void CopyPath(ContainerRow row)
    {
        try
        {
            System.Windows.Clipboard.SetText(row.ConfigFiles ?? "");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another process holds the clipboard; the path is still on screen to select.
        }
    }

    /// <summary>Open the folder compose ran in, where it still exists.</summary>
    /// <param name="row">The project or service.</param>
    public static void Open(ContainerRow row)
    {
        var folder = row.WorkingDir;
        if (folder is null || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            // Named and not inherited (DD261): Explorer outlives the window that asked, and the
            // directory it would hold is the install's own.
            using var _ = Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"")
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.SystemDirectory,
            });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Explorer refused; the folder is still named on screen to open by hand.
        }
    }
}
