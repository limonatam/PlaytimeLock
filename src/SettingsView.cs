using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace PlaytimeLock
{
    /// <summary>
    /// Settings page, built in code (no XAML) so the project builds with just "dotnet build".
    /// Playnite sets DataContext to the PluginSettings instance.
    /// </summary>
    public class SettingsView : UserControl
    {
        public SettingsView()
        {
            var root = new StackPanel { Margin = new Thickness(20) };

            root.Children.Add(new TextBlock
            {
                Text = Loc.Get("SettingsLockHeader"),
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });
            root.Children.Add(new TextBlock
            {
                Text = Loc.Get("SettingsLockHelp"),
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.75,
                Margin = new Thickness(0, 0, 0, 10)
            });
            root.Children.Add(Check(Loc.Get("SettingsAddTag"), nameof(PluginSettings.AddLockTag)));

            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = root
            };
        }

        private static CheckBox Check(string text, string property)
        {
            var box = new CheckBox { Content = text, Margin = new Thickness(0, 0, 0, 8) };
            box.SetBinding(CheckBox.IsCheckedProperty, new Binding(property));
            return box;
        }
    }
}
