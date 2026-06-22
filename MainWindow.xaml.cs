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
            OcTextBox.Text = "honggildong"; // default demo key
            
            // Diagnostics for agy cli on load
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Checking local agy cli connection status...";
            DiagnosticsText.Text = "Checking connection...";
            DiagnosticsText.Foreground = System.Windows.Media.Brushes.Yellow;

            bool isInstalled = await _agyService.CheckAgyCliInstalledAsync();
            if (isInstalled)
            {
                DiagnosticsText.Text = "● agy cli Connected";
                DiagnosticsText.Foreground = System.Windows.Media.Brushes.Green;
                StatusText.Text = "Ready. Please enter your legal inquiry.";
            }
            else
            {
                DiagnosticsText.Text = "● agy cli Disconnected (Check PATH)";
                DiagnosticsText.Foreground = System.Windows.Media.Brushes.Red;
                StatusText.Text = "Warning: Local agy cli is not reachable. Check environment variables.";
                MessageBox.Show(
                    "The 'agy' CLI could not be found or executed in your system PATH.\n" +
                    "Please ensure Google Antigravity CLI is installed and configured, then restart this application.",
                    "agy cli Diagnostics Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ApplySettings_Click(object sender, RoutedEventArgs e)
        {
            var ocKey = OcTextBox.Text.Trim();
            var selectedItem = ModelComboBox.SelectedItem as ComboBoxItem;
            var modelName = selectedItem?.Content?.ToString() ?? "gemini-3.5-flash";

            _mcpService.UpdateConfig(ocKey, "https://korean-law-mcp.fly.dev/mcp");
            _agyService.UpdateModel(modelName);

            StatusText.Text = $"Settings applied. (Model: {modelName})";
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