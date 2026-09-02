using System.Windows;
using System.Windows.Controls;

namespace ManagerIV.Views
{
    public partial class CreditsView : UserControl
    {
        public CreditsView()
        {
            InitializeComponent();
        }

        private void Button_GitHub_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Apply scale transform if screen is small
            double scale = 1.0;
            if (e.NewSize.Width < 950)
            {
                scale = Math.Min(scale, Math.Max(0.85, e.NewSize.Width / 950.0));
            }
            if (e.NewSize.Height < 650)
            {
                scale = Math.Min(scale, Math.Max(0.85, e.NewSize.Height / 650.0));
            }

            if (RootScale != null)
            {
                RootScale.ScaleX = scale;
                RootScale.ScaleY = scale;
            }
        }
    }
}
