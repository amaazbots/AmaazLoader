using System.Windows;
using System.Windows.Input;

namespace AmaazLoader;

public partial class AppleTwoFactorWindow : Window
{
    private string verificationCode =
        string.Empty;

    public AppleTwoFactorWindow(
        string? email)
    {
        InitializeComponent();

        AccountEmailText.Text =
            string.IsNullOrWhiteSpace(
                email)
                ? "Apple Account"
                : email;

        Loaded +=
            AppleTwoFactorWindow_Loaded;
    }

    private void AppleTwoFactorWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        CodeInput.Focus();
    }

    private void VerifyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ErrorText.Visibility =
            Visibility.Collapsed;

        string code =
            CodeInput.Text.Trim();

        if (string.IsNullOrWhiteSpace(
            code))
        {
            ShowError(
                "Enter the verification code from Apple.");

            CodeInput.Focus();

            return;
        }

        if (code.Length != 6)
        {
            ShowError(
                "The Apple verification code must contain 6 digits.");

            CodeInput.Focus();

            CodeInput.SelectAll();

            return;
        }

        if (!code.All(
            char.IsDigit))
        {
            ShowError(
                "The verification code can contain numbers only.");

            CodeInput.Focus();

            CodeInput.SelectAll();

            return;
        }

        verificationCode =
            code;

        CodeInput.Clear();

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

    private void CodeInput_KeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key !=
            Key.Enter)
        {
            return;
        }

        VerifyButton.RaiseEvent(
            new RoutedEventArgs(
                System.Windows.Controls.Button.ClickEvent));

        e.Handled =
            true;
    }

    private void CodeInput_PreviewTextInput(
        object sender,
        TextCompositionEventArgs e)
    {
        e.Handled =
            !e.Text.All(
                char.IsDigit);
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

    public string TakeCode()
    {
        string code =
            verificationCode;

        verificationCode =
            string.Empty;

        return code;
    }

    private void ClearSensitiveData()
    {
        CodeInput.Clear();

        verificationCode =
            string.Empty;
    }
}