using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LawDesktop.Models;
using LawDesktop.Services;

namespace LawDesktop
{
    public partial class MainWindow : Window
    {
        private readonly LawMcpService _mcpService;
        private readonly AgyCliService _agyService;
        private readonly PipelineService _pipelineService;

        // Message list collection for UI binding
        public ObservableCollection<ChatMessage> Messages { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();

            // Initialize service layers
            _mcpService = new LawMcpService();
            _agyService = new AgyCliService();
            _pipelineService = new PipelineService(_mcpService, _agyService);

            // Bind item source
            ChatItemsControl.ItemsSource = Messages;

            // Set default settings value
            OcTextBox.Text = "0428"; // default data.go.kr free api key
            
            // Diagnostics for agy cli on load
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Checking local AI cli connection status...";
            DiagnosticsText.Text = "Checking connection...";
            DiagnosticsText.Foreground = System.Windows.Media.Brushes.Yellow;

            var cliName = await _agyService.GetAvailableCliNameAsync();
            if (!string.IsNullOrEmpty(cliName))
            {
                DiagnosticsText.Text = $"● {cliName} cli Connected";
                DiagnosticsText.Foreground = System.Windows.Media.Brushes.Green;
                StatusText.Text = "Ready. Please enter your legal inquiry.";
            }
            else
            {
                DiagnosticsText.Text = "● Codex/Agy cli Disconnected (Check PATH/Session)";
                DiagnosticsText.Foreground = System.Windows.Media.Brushes.Red;
                StatusText.Text = "Warning: Local AI cli is not reachable. Check environment variables.";
                MessageBox.Show(
                    "Neither the 'codex' nor 'agy' CLI could be found, executed, or authenticated in your system PATH.\n\n" +
                    "1. Please ensure OpenAI Codex CLI or Google Antigravity CLI is installed and configured in PATH.\n" +
                    "2. Open a standard terminal (CMD/PowerShell) and run the CLI once to complete authentication.\n" +
                    "3. Once authentication succeeds, restart this application.",
                    "Local AI cli Diagnostics Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ApplySettings_Click(object sender, RoutedEventArgs e)
        {
            var ocKey = OcTextBox.Text.Trim();

            _mcpService.UpdateConfig(ocKey, "https://korean-law-mcp.fly.dev/mcp");

            StatusText.Text = "Settings applied. (OC Key updated)";
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await ProcessUserQueryAsync();
        }

        private async void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Shift + Enter to newline, Enter to send
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
            {
                e.Handled = true;
                await ProcessUserQueryAsync();
            }
        }

        private async Task ProcessUserQueryAsync()
        {
            var query = InputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;

            // Clear input box
            InputTextBox.Text = string.Empty;

            // 1. Add user query message
            var userMsg = new ChatMessage
            {
                Sender = "User",
                Content = query,
                Timestamp = DateTime.Now
            };
            Messages.Add(userMsg);
            ScrollToBottom();

            // Disable input box to prevent spam
            InputTextBox.IsEnabled = false;

            try
            {
                // 2. Execute RAG Pipeline
                var aiMsg = await _pipelineService.RunPipelineAsync(query, (status, done) =>
                {
                    // Status updates callback
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = status;
                        if (done)
                        {
                            StatusText.Text = "Done";
                        }
                    });
                });

                // 3. Add AI Response message
                Messages.Add(aiMsg);
                ScrollToBottom();
            }
            catch (Exception ex)
            {
                Messages.Add(new ChatMessage
                {
                    Sender = "AI",
                    Content = $"An error occurred: {ex.Message}",
                    GuardSummary = "System Exception Occurred"
                });
                ScrollToBottom();
                StatusText.Text = "Error occurred";
            }
            finally
            {
                InputTextBox.IsEnabled = true;
                InputTextBox.Focus();
            }
        }

        private void ScrollToBottom()
        {
            ChatScrollViewer.ScrollToEnd();
        }

        private void CitationLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Citation citation)
            {
                ViewerTitleText.Text = $"📄 [{citation.Type}] {citation.Title}";
                ViewerSubtitleText.Text = "Ministry of Government Legislation database retrieved.";
                ViewerContentTextBox.Text = citation.Content;
            }
        }
    }
}
