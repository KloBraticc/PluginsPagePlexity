using System.Windows;
using System.Windows.Controls;

public class TestPluginLogic
{
    public TestPluginLogic(Window window)
    {
        var button = (Button)((Grid)window.Content).FindName("MyButton");
        if (button != null)
        {
            button.Click += (s, e) => MessageBox.Show("Button clicked from plugin!");
        }
    }
}
