using System.Windows;
using System.Windows.Input;

namespace AmaazLoader;

public partial class AppleAuthenticationWindow : Window
{
    private string password =
        string.Empty;

    public AppleAuthenticationWindow(
        string email)
    {
        InitializeComponent();

        AccountEmailText.Text =
            string.IsNullOrWhiteSpace(email)
                ? "Apple Account"
                : email;

        Loaded +=
            AppleAuthenticationWindow_Loaded;
    }

    private void AppleAuthenticationWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        PasswordInput.Focus();
    }

    private void AuthenticateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ErrorText.Visibility =
            Visibility.Collapsed;

        string enteredPassword =
            PasswordInput.Password;

        if (string.IsNullOrEmpty(
            enteredPassword))
        {
            ShowError(
                "Enter your Apple Account password.");

            PasswordInput.Focus();

            return;
        }

        password =
            enteredPassword;

        PasswordInput.Clear();

        DialogResult =
            true;

        Close();
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClearPassword();

        DialogResult =
            false;

        Close();
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClearPassword();

        DialogResult =
            false;

        Close();
    }

    private void PasswordInput_KeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        AuthenticateButton.RaiseEvent(
            new RoutedEventArgs(
                System.Windows.Controls.Button.ClickEvent));

        e.Handled =
            true;
    }

    private void TitleBar_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.LeftButton ==
            MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ShowError(
        string message)
    {
        ErrorText.Text =
            message;

        ErrorText.Visibility =
            Visibility.Visible;
    }

    public string TakePassword()
    {
        string result =
            password;

        password =
            string.Empty;

        return result;
    }

    private void ClearPassword()
    {
        PasswordInput.Clear();

        password =
            string.Empty;
    }
}