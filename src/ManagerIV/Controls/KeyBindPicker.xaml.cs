using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ManagerIV.Core;
using Wpf.Ui.Controls;

namespace ManagerIV.Controls;

public partial class KeyBindPicker : UserControl
{
    public static readonly DependencyProperty KeyHexCodeProperty =
        DependencyProperty.Register(
            nameof(KeyHexCode),
            typeof(string),
            typeof(KeyBindPicker),
            new FrameworkPropertyMetadata("0x00", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnKeyHexCodeChanged));

    public string KeyHexCode
    {
        get => (string)GetValue(KeyHexCodeProperty);
        set => SetValue(KeyHexCodeProperty, value);
    }

    private bool _isListening;
    private Window? _subscribedWindow;

    public KeyBindPicker()
    {
        InitializeComponent();
        Loaded += KeyBindPicker_Loaded;
        Unloaded += KeyBindPicker_Unloaded;
    }

    private void KeyBindPicker_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateVisualState();
    }

    private void KeyBindPicker_Unloaded(object sender, RoutedEventArgs e)
    {
        StopListening();
    }

    private static void OnKeyHexCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyBindPicker picker)
        {
            picker.UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        if (_isListening)
        {
            BindText.Text = "Press key...";
            BindText.FontStyle = FontStyles.Italic;
            BindIcon.Symbol = SymbolRegular.Record24;
            BindButton.Appearance = ControlAppearance.Primary;
            ClearButton.Visibility = Visibility.Collapsed;
            ToolTip = "Press any key, mouse button, or press Escape to cancel (Delete/Backspace to unbind).";
            return;
        }

        string hex = KeyHexCode;
        int vk = VirtualKeyHelper.ParseVirtualKey(hex);
        string displayName = VirtualKeyHelper.GetKeyDisplayName(vk);

        BindText.FontStyle = FontStyles.Normal;
        BindButton.Appearance = ControlAppearance.Secondary;

        if (vk == 0)
        {
            BindText.Text = "Unbound";
            BindText.Foreground = TryFindResource("TextFillColorTertiaryBrush") as Brush ?? Brushes.Gray;
            BindIcon.Symbol = SymbolRegular.DismissCircle24;
            ClearButton.Visibility = Visibility.Collapsed;
            ToolTip = "Unbound. Click to assign a key or mouse button.";
        }
        else
        {
            BindText.Text = displayName;
            BindText.Foreground = TryFindResource("TextFillColorPrimaryBrush") as Brush ?? Brushes.White;
            
            // Mouse button (0x01 - 0x06) or keyboard key
            if (vk >= 0x01 && vk <= 0x06)
            {
                BindIcon.Symbol = SymbolRegular.Cursor24;
            }
            else
            {
                BindIcon.Symbol = SymbolRegular.Keyboard24;
            }

            ClearButton.Visibility = Visibility.Visible;
            ToolTip = $"Bound to {displayName} ({VirtualKeyHelper.FormatVirtualKey(vk)}). Click to rebind.";
        }
    }

    private void BindButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isListening)
        {
            StopListening();
        }
        else
        {
            StartListening();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        StopListening();
        KeyHexCode = VirtualKeyHelper.UnboundHex;
    }

    private void StartListening()
    {
        _isListening = true;
        UpdateVisualState();

        // Delay attaching to window events to ensure initial mouse click finishes
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_isListening) return;

            _subscribedWindow = Window.GetWindow(this);
            if (_subscribedWindow != null)
            {
                _subscribedWindow.PreviewKeyDown += Window_PreviewKeyDown;
                _subscribedWindow.PreviewMouseDown += Window_PreviewMouseDown;
                _subscribedWindow.Deactivated += Window_Deactivated;
            }
        }));
    }

    private void StopListening()
    {
        if (!_isListening) return;

        _isListening = false;
        if (_subscribedWindow != null)
        {
            _subscribedWindow.PreviewKeyDown -= Window_PreviewKeyDown;
            _subscribedWindow.PreviewMouseDown -= Window_PreviewMouseDown;
            _subscribedWindow.Deactivated -= Window_Deactivated;
            _subscribedWindow = null;
        }

        UpdateVisualState();
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        StopListening();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            StopListening();
            return;
        }

        if (key == Key.Delete || key == Key.Back)
        {
            KeyHexCode = VirtualKeyHelper.UnboundHex;
            StopListening();
            return;
        }

        int vk = KeyInterop.VirtualKeyFromKey(key);
        vk = VirtualKeyHelper.NormalizeModifierVirtualKey(vk);

        if (vk > 0)
        {
            KeyHexCode = VirtualKeyHelper.FormatVirtualKey(vk);
        }

        StopListening();
    }

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        int vk = e.ChangedButton switch
        {
            MouseButton.Left => 0x01,     // VK_LBUTTON
            MouseButton.Right => 0x02,    // VK_RBUTTON
            MouseButton.Middle => 0x04,   // VK_MBUTTON
            MouseButton.XButton1 => 0x05, // VK_XBUTTON1 (Mouse 4)
            MouseButton.XButton2 => 0x06, // VK_XBUTTON2 (Mouse 5)
            _ => 0
        };

        if (vk > 0)
        {
            KeyHexCode = VirtualKeyHelper.FormatVirtualKey(vk);
        }

        StopListening();
    }
}
