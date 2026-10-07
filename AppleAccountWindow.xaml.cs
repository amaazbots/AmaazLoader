using System.Windows;
using System.Windows.Input;

namespace AmaazLoader;

public partial class AppleAccountWindow : Window
{
    public string AppleEmail { get; private set; } = "";

    public string ApplePassword { get; set; } = "";

    public AppleAccountWindow(
        string? existingEmail = null)
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(
            existingEmail))
        {
            EmailTextBox.Text =
                existingEmail;
        }

        Loaded +=
            AppleAccountWindow_Loaded;
    }

    private void AppleAccountWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(
            EmailTextBox.Text))
        {
            EmailTextBox.Focus();
        }
        else
        {
            PasswordInput.Focus();
        }
    }

    private void ConnectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ErrorText.Visibility =
            Visibility.Collapsed;

        string email =
            EmailTextBox.Text.Trim();

        string password =
            PasswordInput.Password;

        if (string.IsNullOrWhiteSpace(
            email))
        {
            ShowError(
                "Enter your Apple Account email address.");

            EmailTextBox.Focus();

            return;
        }

        if (!email.Contains('@'))
        {
            ShowError(
                "Enter a valid Apple Account email address.");

            EmailTextBox.Focus();

            return;
        }

        if (string.IsNullOrEmpty(
            password))
        {
            ShowError(
                "Enter your Apple Account password.");

            PasswordInput.Focus();

            return;
        }

        AppleEmail =
            email;

        ApplePassword =
            password;

        PasswordInput.Clear();

        DialogResult =
            true;

        Close();
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClearSensitiveData();

        DialogResult =
            false;

        Close();
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClearSensitiveData();

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

        ConnectButton.RaiseEvent(
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

    private void ClearSensitiveData()
    {
        PasswordInput.Clear();

        ApplePassword =
            "";
    }
}