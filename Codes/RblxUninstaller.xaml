using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

public class TestPluginLogic
{
    private Window _window;
    private Button _button;

    public TestPluginLogic(Window window)
    {
        _window = window;

        // Find the button inside the Grid (not DockPanel)
        _button = (Button)((Grid)window.Content).FindName("MyButton");

        if (_button != null)
        {
            _button.Click += async (s, e) => await UninstallButton_Click();
        }
        else
        {
            MessageBox.Show("Button not found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task UninstallButton_Click()
    {
        var result = MessageBox.Show(
            "This will permanently remove Roblox and all its data. Are you sure you want to continue?",
            "Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _button.IsEnabled = false;
        _button.Content = "Uninstalling...";

        await Task.Run(() =>
        {
            TryKillRobloxProcesses();
            TryRunOfficialUninstaller();
            TryDeleteRobloxFolders();
            TryCleanRegistry();
            TryDeleteShortcuts();
        });

        _button.Content = "Finished!";

        MessageBox.Show("Roblox has been completely uninstalled.\nIt is recommended to restart your computer.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Helpers simplified: just MessageBoxes on errors, no UI logging

    private void TryKillRobloxProcesses()
    {
        string[] processNames = { "RobloxPlayerBeta", "RobloxPlayerLauncher", "RobloxStudioBeta", "RobloxStudioLauncher" };
        foreach (var name in processNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                foreach (var proc in procs)
                {
                    proc.Kill();
                    proc.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error terminating process {name}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void TryRunOfficialUninstaller()
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string robloxVersionsPath = Path.Combine(localAppData, "Roblox", "Versions");
            if (!Directory.Exists(robloxVersionsPath)) return;

            var uninstallerPath = Directory.GetFiles(robloxVersionsPath, "RobloxPlayerUninstaller.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (uninstallerPath != null)
            {
                var proc = Process.Start(uninstallerPath);
                if (proc != null)
                {
                    proc.WaitForExit(30000);
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error running official uninstaller: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TryDeleteRobloxFolders()
    {
        try
        {
            var folders = new List<string>()
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Roblox"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "RbxLogs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Roblox"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Roblox")
            };

            foreach (var folder in folders)
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error deleting Roblox folders: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TryCleanRegistry()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", true))
            {
                if (key != null)
                {
                    DeleteRegistryKeySafe(key, "Roblox");
                    DeleteRegistryKeySafe(key, "Roblox Corporation");
                }
            }
            using (RegistryKey classesRoot = Registry.ClassesRoot)
            {
                DeleteRegistryKeySafe(classesRoot, "roblox-player");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cleaning registry: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteRegistryKeySafe(RegistryKey parentKey, string keyName)
    {
        try
        {
            if (parentKey.OpenSubKey(keyName) != null)
                parentKey.DeleteSubKeyTree(keyName);
        }
        catch
        {
            // Ignore errors deleting registry keys
        }
    }

    private void TryDeleteShortcuts()
    {
        try
        {
            var shortcuts = new List<string>()
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Roblox Player.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Roblox Studio.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Roblox"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Roblox Player.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Roblox Studio.lnk")
            };

            foreach (var path in shortcuts)
            {
                if (File.Exists(path))
                    File.Delete(path);
                else if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
        }
        catch
        {
            // Ignore errors deleting shortcuts
        }
    }
}
