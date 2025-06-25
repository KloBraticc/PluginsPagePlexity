using System.Windows;
using System.Windows.Controls;

namespace YourNamespace
{
    public partial class CustomUIButton : UserControl
    {
        public CustomUIButton()
        {
            InitializeComponent();
            MyButton.Click += MyButton_Click;
        }

        private void MyButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Hello from the custom plugin button!");
        }
    }
}
