using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

public class TestPluginLogic
{
    public TestPluginLogic(Window window)
    {
        var button = (Button)((Grid)window.Content).FindName("MyButton");
        if (button != null)
            button.Click += (s, e) =>
            {
                var di = new DirectoryInfo(Path.GetTempPath());
                foreach (var f in di.GetFiles())
                {
                    try { f.Delete(); } catch { }
                }
                foreach (var d in di.GetDirectories())
                {
                    try { d.Delete(true); } catch { }
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
                MessageBox.Show("PC optimized!");
            };
    }
}
