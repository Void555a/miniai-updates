using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MiniAIApp
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, Dictionary<string, Dictionary<string, int>>> themedModel = new();
        private readonly string ModelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniAI", "miniai.db");
        private readonly string UpdatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniAI", "update_ready.flag");
        private bool isAdmin = false;
        private bool isAutoLearning = false;
        private bool isSearchMode = false; 
        private bool isCodeSearchMode = false;
        private Random rng = new();
        private HttpClient http = new();
        private DispatcherTimer autoLearnTimer;
        private StackPanel chatPanel;
        private TextBox chatInput;
        private ScrollViewer scrollViewer;
        private Grid mainGrid;
        private Grid chatGrid;
        private Border adminPanel;
        private PasswordBox adminPassBox;
        private StackPanel adminControlsPanel;
        private Button searchModeBtn;
        private Button codeSearchBtn;
        private bool waitingForPassword = false;
        private string[] randomTopics = { "Кошка", "Собака", "Москва", "Россия", "Солнце", "Луна", "Вода", "Литература", "Наука", "История", "Космос", "Музыка", "Кино", "Спорт" };

        public MainWindow()
        {
            Title = "MiniAI"; Width = 1000; Height = 750;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 24));
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            try { this.Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/logo.png")); } catch { }
            CheckForUpdates(); LoadModel();
            autoLearnTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            autoLearnTimer.Tick += async (s, e) => await AutoLearnTick();
            mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var topPanel = new Grid { Margin = new Thickness(15) };
            var helpBtn = new Button { Content = "?", Width = 30, Height = 30, Background = Brushes.Transparent, Foreground = Brushes.Gray, BorderThickness = new Thickness(0), FontSize = 18, Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left };
            helpBtn.Click += (s, e) => ShowHelp();
            var topLogo = new Image { Width = 24, Height = 24, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            try { topLogo.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/logo.png")); } catch { }
            var topLeftStack = new StackPanel { Orientation = Orientation.Horizontal };
            topLeftStack.Children.Add(topLogo); topLeftStack.Children.Add(helpBtn);
            topPanel.Children.Add(topLeftStack);
            Grid.SetRow(topPanel, 0); Grid.SetZIndex(topPanel, 10); mainGrid.Children.Add(topPanel);
            var centerPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var centerLogo = new Image { Width = 150, Height = 150, Margin = new Thickness(0, 0, 0, 10), HorizontalAlignment = HorizontalAlignment.Center };
            try { centerLogo.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/logo.png")); } catch { }
            centerPanel.Children.Add(centerLogo);
            var logoText = new TextBlock { Text = "miniai", FontSize = 48, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40)), HorizontalAlignment = HorizontalAlignment.Center };
            centerPanel.Children.Add(logoText);
            var inputBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(38, 38, 38)), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 30, 0, 10), Width = 650, Padding = new Thickness(10) };
            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var inputBox = new TextBox { Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 16, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalContentAlignment = VerticalAlignment.Center, MinHeight = 40 };
            inputBox.KeyDown += (s, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift) { e.Handled = true; StartChat(inputBox.Text); inputBox.Clear(); } };
            Grid.SetColumn(inputBox, 0); inputGrid.Children.Add(inputBox);
            var sendBtn = new Button { Content = "↑", Width = 34, Height = 34, Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Bottom };
            sendBtn.Click += (s, e) => { StartChat(inputBox.Text); inputBox.Clear(); };
            Grid.SetColumn(sendBtn, 1); inputGrid.Children.Add(sendBtn);
            inputBorder.Child = inputGrid; centerPanel.Children.Add(inputBorder);
            var modelRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            modelRow.Children.Add(new TextBlock { Text = "📦 MiniAI 1.0", Foreground = Brushes.Gray, FontSize = 14 });
            centerPanel.Children.Add(modelRow);
            var centerGrid = new Grid(); centerGrid.Children.Add(centerPanel);
            Grid.SetRow(centerGrid, 0); mainGrid.Children.Add(centerGrid);
            chatGrid = new Grid { Visibility = Visibility.Collapsed, Margin = new Thickness(20) };
            chatGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            chatGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            chatPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            scrollViewer.Content = chatPanel; Grid.SetRow(scrollViewer, 0); chatGrid.Children.Add(scrollViewer);
            var chatInputBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(38, 38, 38)), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 10, 0, 0) };
            var chatInputGrid = new Grid();
            chatInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            chatInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            chatInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            chatInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            chatInput = new TextBox { Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 16, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 40 };
            chatInput.KeyDown += (s, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift) { e.Handled = true; SendChatMessage(chatInput.Text); chatInput.Clear(); } };
            Grid.SetColumn(chatInput, 0); chatInputGrid.Children.Add(chatInput);
            searchModeBtn = new Button { Content = "🔍", Width = 34, Height = 34, Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(5, 0, 5, 0) };
            searchModeBtn.Click += (s, e) => { isSearchMode = !isSearchMode; isCodeSearchMode = false; searchModeBtn.Background = isSearchMode ? new SolidColorBrush(Color.FromRgb(0, 120, 215)) : new SolidColorBrush(Color.FromRgb(50, 50, 50)); codeSearchBtn.Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)); chatInput.Focus(); };
            Grid.SetColumn(searchModeBtn, 1); chatInputGrid.Children.Add(searchModeBtn);
            codeSearchBtn = new Button { Content = "🎮", Width = 34, Height = 34, Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 5, 0) };
            codeSearchBtn.Click += (s, e) => { isCodeSearchMode = !isCodeSearchMode; isSearchMode = false; codeSearchBtn.Background = isCodeSearchMode ? new SolidColorBrush(Color.FromRgb(128, 0, 128)) : new SolidColorBrush(Color.FromRgb(50, 50, 50)); searchModeBtn.Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)); chatInput.Focus(); };
            Grid.SetColumn(codeSearchBtn, 2); chatInputGrid.Children.Add(codeSearchBtn);
            var chatSendBtn = new Button { Content = "↑", Width = 34, Height = 34, Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Bottom };
            chatSendBtn.Click += (s, e) => { SendChatMessage(chatInput.Text); chatInput.Clear(); };
            Grid.SetColumn(chatSendBtn, 3); chatInputGrid.Children.Add(chatSendBtn);
            chatInputBorder.Child = chatInputGrid; Grid.SetRow(chatInputBorder, 1); chatGrid.Children.Add(chatInputBorder);
            Grid.SetRow(chatGrid, 0); mainGrid.Children.Add(chatGrid);
            adminControlsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10), Visibility = Visibility.Collapsed };
            var autoLearnBtn = new Button { Content = "Вкл Авто-обучение", Background = new SolidColorBrush(Color.FromRgb(50, 80, 50)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(5), Cursor = Cursors.Hand };
            autoLearnBtn.Click += (s, e) => { isAutoLearning = !isAutoLearning; if (isAutoLearning) { autoLearnBtn.Content = "Выкл Авто-обучение"; autoLearnBtn.Background = new SolidColorBrush(Color.FromRgb(80, 50, 50)); autoLearnTimer.Start(); AddMessage("Система", "Авто-обучение включено.", true); } else { autoLearnBtn.Content = "Вкл Авто-обучение"; autoLearnBtn.Background = new SolidColorBrush(Color.FromRgb(50, 80, 50)); autoLearnTimer.Stop(); AddMessage("Система", "Авто-обучение выключено.", true); } };
            adminControlsPanel.Children.Add(autoLearnBtn);
            var updateBtn = new Button { Content = "Update for users", Background = new SolidColorBrush(Color.FromRgb(50, 100, 50)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(5), Cursor = Cursors.Hand };
            updateBtn.Click += (s, e) => { File.WriteAllText(UpdatePath, "true"); AddMessage("Система", "Обновление разослано!", true); };
            adminControlsPanel.Children.Add(updateBtn);
            Grid.SetRow(adminControlsPanel, 1); mainGrid.Children.Add(adminControlsPanel);
            adminPanel = new Border { Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), BorderBrush = Brushes.DarkGray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Width = 300, Height = 100, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Visibility = Visibility.Collapsed };
            var adminStack = new StackPanel { Margin = new Thickness(15) };
            adminStack.Children.Add(new TextBlock { Text = "Введите пароль администратора:", Foreground = Brushes.White, Margin = new Thickness(0,0,0,5) });
            adminPassBox = new PasswordBox { PasswordChar = '*' };
            adminPassBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) CheckAdminPassword(); };
            adminStack.Children.Add(adminPassBox); adminPanel.Child = adminStack; mainGrid.Children.Add(adminPanel);
            this.PreviewKeyDown += (s, e) => { if (e.Key == Key.F11) { WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized; } if (e.Key == Key.Escape && WindowState == WindowState.Maximized) { WindowStyle = WindowStyle.SingleBorderWindow; WindowState = WindowState.Normal; } if (e.Key == Key.A) waitingForPassword = true; if (e.Key == Key.D && waitingForPassword) { waitingForPassword = false; if (!isAdmin) adminPanel.Visibility = Visibility.Visible; } if (e.Key != Key.A) waitingForPassword = false; };
            Content = mainGrid;
        }
        private void CheckAdminPassword() { if (adminPassBox.Password == "5656") { isAdmin = true; adminPanel.Visibility = Visibility.Collapsed; adminControlsPanel.Visibility = Visibility.Visible; AddMessage("Система", "Админка активирована!", true); } else adminPassBox.Clear(); }
        private async Task AutoLearnTick() { if (!isAdmin || !isAutoLearning) return; string topic = randomTopics[rng.Next(randomTopics.Length)]; await LearnFromUrl("https://ru.wikipedia.org/wiki/" + topic, true); }
        private void CheckForUpdates() { if (File.Exists(UpdatePath)) { var updateWindow = new Window { Title = "Updating...", Width = 400, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = new SolidColorBrush(Color.FromRgb(24, 24, 24)), WindowStyle = WindowStyle.None }; var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }; stack.Children.Add(new TextBlock { Text = "Updating ---", Foreground = Brushes.White, FontSize = 24, FontWeight = FontWeights.Bold }); updateWindow.Content = stack; updateWindow.Show(); var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) }; timer.Tick += (s, e) => { timer.Stop(); updateWindow.Close(); File.Delete(UpdatePath); }; timer.Start(); } }
        private void ShowHelp() { var helpWin = new Window { Title = "Help", Width = 550, Height = 550, Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), WindowStartupLocation = WindowStartupLocation.CenterScreen }; var stack = new StackPanel { Margin = new Thickness(20) }; stack.Children.Add(new TextBlock { Text = "Команды MiniAI", Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,10) }); string[] cmds = { "• Ссылка (https://...) — обучение (Админ)", "• /warp — очистить ВСЮ память (Админ)", "• /warp <url> — удалить знания ТОЛЬКО по теме (Админ)", "• /stats — статистика (Админ)", "• /lm <url> <слов> — обучить N слов (Админ)", "• /calc <пример> — калькулятор", "• /search <текст> — поиск в Википедии и ссылки в браузере", "• 🔍 — кнопка поиска", "• 🎮 — кнопка поиска кодов для игр", "• Правда? / Да или нет — умный ответ", "• A + D — ввод пароля (5656)", "• F11 — полный экран" }; foreach (var c in cmds) stack.Children.Add(new TextBlock { Text = c, Foreground = Brushes.LightGray, Margin = new Thickness(0, 5, 0, 5), TextWrapping = TextWrapping.Wrap }); helpWin.Content = stack; helpWin.Show(); }
        private void StartChat(string text) { if (string.IsNullOrWhiteSpace(text)) return; var center = mainGrid.Children.OfType<Grid>().FirstOrDefault(g => g != chatGrid && g != mainGrid.Children[0]); if (center != null) center.Visibility = Visibility.Collapsed; chatGrid.Visibility = Visibility.Visible; SendChatMessage(text); }
        
        private async Task<bool> TryAnswerYesNo(string text) {
            string lowerText = text.ToLower().Trim();
            string[] triggers = { "правда", "реально", "да", "нет" };
            string matchedTrigger = null;
            foreach (var t in triggers) {
                if (lowerText.StartsWith(t + " ") || lowerText == t + "?") {
                    matchedTrigger = t;
                    break;
                }
            }
            
            if (matchedTrigger == null) return false;

            string query = lowerText.Substring(matchedTrigger.Length).Trim().TrimStart('?', ' ', ',').TrimEnd('?', ' ', '.', ',');
            
            if (string.IsNullOrEmpty(query)) {
                AddMessage("MiniAI", "О чем именно? Напиши: правда [твой вопрос]?", true);
                return true;
            }

            var thinking = AddThinkingMessage("Проверяю... 🔍");
            try {
                string wikiResult = await SearchOnWiki(query);
                if (!string.IsNullOrEmpty(wikiResult)) {
                    string shortFact = wikiResult;
                    if (shortFact.Length > 150) {
                        shortFact = shortFact.Substring(0, 150);
                        int lastSpace = shortFact.LastIndexOf(' ');
                        if (lastSpace > 0) shortFact = shortFact.Substring(0, lastSpace);
                        shortFact += "...";
                    }
                    UpdateMessage(thinking, "Да! " + shortFact);
                } else {
                    UpdateMessage(thinking, "Нет! Я не нашел подтверждения этому в интернете.");
                }
            } catch {
                UpdateMessage(thinking, "Нет! Произошла ошибка при поиске.");
            }
            return true;
        }

        private async void SendChatMessage(string text) { 
            if (string.IsNullOrWhiteSpace(text)) return; 
            AddMessage("Вы", text, false); 
            bool isYesNo = await TryAnswerYesNo(text);
            if (isYesNo) return;
            string calcResult = TryCalculate(text); 
            if (calcResult != null) { AddMessage("MiniAI", calcResult, true); return; } 
            if (text.StartsWith("/search ")) { string query = text.Substring(8).Trim(); var thinking = AddThinkingMessage("Thinking... 🔍"); await PerformSmartSearch(query, thinking); return; } 
            if (isCodeSearchMode) { isCodeSearchMode = false; codeSearchBtn.Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)); var thinking = AddThinkingMessage("Ищу коды в поисковиках... 🎮"); string res = await SearchOnWebForCode(text); UpdateMessage(thinking, res != null ? res : "Не удалось найти коды. Проверь название игры."); return; } 
            if (isSearchMode) { isSearchMode = false; searchModeBtn.Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)); var thinking = AddThinkingMessage("Thinking... 🔍"); await PerformSmartSearch(text, thinking); return; } 
            if (text == "/warp") { if (!isAdmin) { AddMessage("Система", "Отказано.", true); return; } themedModel.Clear(); SaveModel(); AddMessage("Система", "Вся память очищена.", true); } 
            else if (text.StartsWith("/warp ")) { if (!isAdmin) { AddMessage("Система", "Отказано.", true); return; } string url = text.Substring(6).Trim(); string topicName = GetTopicName(url); if (themedModel.ContainsKey(topicName)) { themedModel.Remove(topicName); SaveModel(); AddMessage("Система", "Тема '" + topicName + "' удалена.", true); } else AddMessage("Система", "Тема не найдена.", true); } 
            else if (text == "/stats") { int total = themedModel.Sum(t => t.Value.Count); AddMessage("MiniAI", "Тем: " + themedModel.Count + ", контекстов: " + total, true); } 
            else if (text.StartsWith("/lm ")) { if (!isAdmin) { AddMessage("Система", "Отказано.", true); return; } await LearnFromUrl(text.Substring(4), false, true); } 
            else if (text.StartsWith("http://") || text.StartsWith("https://")) { if (!isAdmin) { AddMessage("Система", "Отказано.", true); return; } await LearnFromUrl(text, false); } 
            else { string answer = Generate(text); AddMessage("MiniAI", answer, true); if (isAdmin) { TrainOnText(text + " " + answer, "chat"); SaveModel(); } } 
        }
        
        private async Task PerformSmartSearch(string query, TextBlock thinkingBubble) { string wikiResult = await SearchOnWiki(query); string webLinks = await SearchOnWebForLinks(query); StringBuilder finalResponse = new StringBuilder(); if (!string.IsNullOrEmpty(wikiResult)) { finalResponse.AppendLine("Вот что я нашел в Википедии:"); finalResponse.AppendLine(wikiResult); finalResponse.AppendLine(); } if (!string.IsNullOrEmpty(webLinks)) { finalResponse.AppendLine("Вот еще что я смог найти в интернете:"); finalResponse.AppendLine(webLinks); } if (finalResponse.Length == 0) { UpdateMessage(thinkingBubble, "Ничего не найдено. Попробуй переформулировать запрос."); } else { UpdateMessage(thinkingBubble, finalResponse.ToString().Trim()); } }
        private async Task<string> SearchOnWebForCode(string query) { try { string searchUrl = "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query); http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0"); string html = await http.GetStringAsync(searchUrl); var matches = Regex.Matches(html, @"<a rel=""nofollow"" class=""result__a"" href=""([^""]+)"">(.*?)</a>"); if (matches.Count > 0) { var results = new StringBuilder(); results.AppendLine("Нашел такие результаты:\n"); for (int i = 0; i < Math.Min(3, matches.Count); i++) { string rawUrl = matches[i].Groups[1].Value; string title = Regex.Replace(matches[i].Groups[2].Value, "<.*?>", ""); if (rawUrl.Contains("uddg=")) { int start = rawUrl.IndexOf("uddg=") + 5; int end = rawUrl.IndexOf("&", start); if (end == -1) end = rawUrl.Length; rawUrl = Uri.UnescapeDataString(rawUrl.Substring(start, end - start)); } results.AppendLine("• " + title); results.AppendLine("  " + rawUrl + "\n"); } return results.ToString(); } } catch { } return null; }
        private async Task<string> SearchOnWebForLinks(string query) { try { string searchUrl = "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query); http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0"); string html = await http.GetStringAsync(searchUrl); var matches = Regex.Matches(html, @"<a rel=""nofollow"" class=""result__a"" href=""([^""]+)"">(.*?)</a>"); if (matches.Count > 0) { var results = new StringBuilder(); for (int i = 0; i < Math.Min(3, matches.Count); i++) { string rawUrl = matches[i].Groups[1].Value; string title = Regex.Replace(matches[i].Groups[2].Value, "<.*?>", ""); if (rawUrl.Contains("uddg=")) { int start = rawUrl.IndexOf("uddg=") + 5; int end = rawUrl.IndexOf("&", start); if (end == -1) end = rawUrl.Length; rawUrl = Uri.UnescapeDataString(rawUrl.Substring(start, end - start)); } results.AppendLine("• " + title); results.AppendLine("  " + rawUrl); } return results.ToString(); } } catch { } return null; }
        private async Task<string> SearchOnWiki(string query) { try { string searchUrl = "https://ru.wikipedia.org/w/api.php?action=query&list=search&srsearch=" + Uri.EscapeDataString(query) + "&format=json&utf8=1"; http.DefaultRequestHeaders.UserAgent.ParseAdd("MiniAI/1.0"); string json = await http.GetStringAsync(searchUrl); using var doc = JsonDocument.Parse(json); var searchResults = doc.RootElement.GetProperty("query").GetProperty("search"); if (searchResults.GetArrayLength() > 0) { string title = searchResults[0].GetProperty("title").GetString(); return await FetchWikiText(title); } } catch { } return null; }
        private async Task<string> FetchWikiText(string title) { try { string api = "https://ru.wikipedia.org/w/api.php?action=query&prop=extracts&explaintext=1&format=json&redirects=1&titles=" + Uri.EscapeDataString(title); string json = await http.GetStringAsync(api); using var doc = JsonDocument.Parse(json); var pages = doc.RootElement.GetProperty("query").GetProperty("pages"); foreach (var page in pages.EnumerateObject()) { if (page.Value.TryGetProperty("extract", out var ex)) { string rawText = ex.GetString() ?? ""; if (string.IsNullOrWhiteSpace(rawText)) return null; var words = Tokenize(rawText).Take(150).ToList(); return string.Join(" ", words) + "..."; } } } catch { } return null; }
        private TextBlock AddThinkingMessage(string text) { var textBlock = new TextBlock { Text = text, Foreground = Brushes.Gray, FontSize = 15, FontStyle = FontStyles.Italic, TextWrapping = TextWrapping.Wrap }; var bubble = new Border { Background = new SolidColorBrush(Color.FromRgb(35, 35, 35)), CornerRadius = new CornerRadius(10), Padding = new Thickness(15), Margin = new Thickness(0, 0, 0, 10), HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = 800 }; bubble.Child = textBlock; chatPanel.Children.Add(bubble); scrollViewer.ScrollToEnd(); return textBlock; }
        private void UpdateMessage(TextBlock tb, string newText) { tb.Text = newText; tb.FontStyle = FontStyles.Normal; tb.Foreground = Brushes.White; scrollViewer.ScrollToEnd(); }
        private string TryCalculate(string text) { string expr = text.Trim(); if (expr.StartsWith("/calc ")) expr = expr.Substring(6).Trim(); expr = expr.ToLower().Replace("умножить на", "*").Replace("умножить", "*").Replace("плюс", "+").Replace("минус", "-").Replace("разделить на", "/").Replace("делить на", "/").Replace(",", "."); if (Regex.IsMatch(expr, @"^[\d\s\+\-\*\/\(\)\.]+$") && Regex.IsMatch(expr, @"[\+\-\*\/]")) { try { var result = new DataTable().Compute(expr, null); if (result is double d && d % 1 == 0) result = (int)d; return "Результат: " + result; } catch { return "Ошибка в выражении."; } } return null; }
        private void AddMessage(string sender, string message, bool isAI) { bool isCode = message.Contains("[HTML]") || message.Contains("[C#]") || message.Contains("http"); var textBlock = new TextBlock { Text = message, Foreground = Brushes.White, FontSize = isCode ? 13 : 15, TextWrapping = TextWrapping.Wrap, FontFamily = isCode ? new FontFamily("Consolas") : new FontFamily("Segoe UI") }; var bubble = new Border { Background = isAI ? new SolidColorBrush(Color.FromRgb(45, 45, 45)) : new SolidColorBrush(Color.FromRgb(60, 80, 120)), CornerRadius = new CornerRadius(10), Padding = new Thickness(15), Margin = new Thickness(0, 0, 0, 10), HorizontalAlignment = isAI ? HorizontalAlignment.Left : HorizontalAlignment.Right, MaxWidth = 800 }; bubble.Child = textBlock; if (isAI) { var avatar = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top, ClipToBounds = true }; try { var img = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/logo.png")), Stretch = Stretch.UniformToFill }; avatar.Child = img; } catch { } var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10) }; row.Children.Add(avatar); bubble.Margin = new Thickness(0); row.Children.Add(bubble); chatPanel.Children.Add(row); } else { chatPanel.Children.Add(bubble); } scrollViewer.ScrollToEnd(); }
        private string GetTopicName(string url) { string cleanUrl = url.Split('?')[0].Split('#')[0]; string rawTitle = cleanUrl.Split('/').Last(); return Uri.UnescapeDataString(rawTitle).Replace("_", " "); }
        private async Task LearnFromUrl(string input, bool isAuto, bool isLm = false) { string url = input; int limit = 500; if (isLm) { var parts = input.Split(' '); url = parts[0]; if (parts.Length > 1) int.TryParse(parts[1], out limit); } string topicName = GetTopicName(url); if (!isAuto) AddMessage("Система", "Скачиваю и чищу: " + url + "...", true); try { string text = await FetchTextFromUrl(url); if (string.IsNullOrEmpty(text) || text.Length < 100) { if (!isAuto) AddMessage("Система", "Мало текста.", true); return; } var words = Tokenize(text).ToList(); if (words.Count > limit) words = words.Take(limit).ToList(); TrainOnText(string.Join(" ", words), topicName); SaveModel(); if (!isAuto) AddMessage("Система", "Успех! Изучена тема '" + topicName + "' (" + words.Count + " слов).", true); else AddMessage("Система", "[Авто] Изучена тема: " + topicName, true); } catch (Exception ex) { if (!isAuto) AddMessage("Система", "Ошибка: " + ex.Message, true); } }
        private async Task<string> FetchTextFromUrl(string url) { if (url.Contains("wikipedia.org/wiki/")) { string cleanUrl = url.Split('?')[0]; var title = cleanUrl.Split(new[] { "/wiki/" }, StringSplitOptions.None)[1]; title = Uri.UnescapeDataString(title).Replace('_', ' '); return await FetchWikiText(title); } http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0"); string html = await http.GetStringAsync(url); html = Regex.Replace(html, @"<script[\s\S]*?</script>", " "); html = Regex.Replace(html, @"<style[\s\S]*?</style>", " "); html = Regex.Replace(html, @"<[^>]+>", " "); return System.Net.WebUtility.HtmlDecode(html); }
        private string Generate(string seed) { if (themedModel.Count == 0) return "Я пока ничего не знаю."; string query = seed.ToLowerInvariant().Trim(); var words = Tokenize(seed).ToList(); string chosenTheme = "chat"; foreach (var theme in themedModel.Keys) { if (theme.ToLower().Contains(query) || query.Contains(theme.ToLower()) || words.Any(w => theme.ToLower().Contains(w))) { chosenTheme = theme; break; } } if (chosenTheme == "chat" && !themedModel.ContainsKey("chat")) chosenTheme = themedModel.Keys.ElementAt(rng.Next(themedModel.Count)); var localModel = themedModel[chosenTheme]; if (words.Count < 2) { var startKey = localModel.Keys.ElementAt(rng.Next(localModel.Count)); words = startKey.Split(' ').ToList(); } var result = new List<string>(words); var recent = new Queue<string>(); foreach (var w in words) { recent.Enqueue(w); if (recent.Count > 3) recent.Dequeue(); } for (int i = 0; i < 25; i++) { string ctx = words[words.Count - 2] + " " + words[words.Count - 1]; if (!localModel.TryGetValue(ctx, out var nexts) || nexts.Count == 0) break; string next = null; foreach (var kv in nexts.OrderByDescending(k => k.Value)) { if (!recent.Contains(kv.Key)) { next = kv.Key; break; } } if (next == null) break; result.Add(next); words.Add(next); recent.Enqueue(next); if (recent.Count > 3) recent.Dequeue(); if (next.EndsWith(".") || next.EndsWith("!")) break; } return string.Join(" ", result); }
        private void TrainOnText(string text, string theme) { if (!themedModel.ContainsKey(theme)) themedModel[theme] = new Dictionary<string, Dictionary<string, int>>(); var localModel = themedModel[theme]; var words = Tokenize(text).ToList(); for (int i = 0; i < words.Count - 2; i++) { string ctx = words[i] + " " + words[i + 1]; string next = words[i + 2]; if (!localModel.ContainsKey(ctx)) localModel[ctx] = new Dictionary<string, int>(); localModel[ctx][next] = (localModel[ctx].ContainsKey(next) ? localModel[ctx][next] : 0) + 1; } }
        private IEnumerable<string> Tokenize(string text) => Regex.Matches(text.ToLowerInvariant(), @"[а-яё0-9]+").Cast<Match>().Select(m => m.Value);
        private void LoadModel() { if (!File.Exists(ModelPath)) { GenerateDefaultKnowledge(); return; } try { var json = File.ReadAllText(ModelPath, Encoding.UTF8); var loaded = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, int>>>>(json); if (loaded != null) themedModel = loaded; } catch { } }
        private void GenerateDefaultKnowledge() { TrainOnText("привет как дела хорошо а у тебя нормально спасибо я маленький ии miniai учусь на текстах", "chat"); TrainOnText("вода это прозрачная жидкость без цвета и запаха она необходима для жизни всех живых существ на земле", "Вода"); TrainOnText("кошка это домашнее животное которое любит молоко и мышей кошки очень любят спать и играть", "Кошка"); TrainOnText("собака это верный друг человека собаки охраняют дом и любят гулять", "Собака"); TrainOnText("москва это столица россии большой город с красной площадью и кремлем", "Москва"); SaveModel(); }
        private void SaveModel() { try { string dir = Path.GetDirectoryName(ModelPath); if (dir != null) Directory.CreateDirectory(dir); File.WriteAllText(ModelPath, JsonSerializer.Serialize(themedModel), Encoding.UTF8); } catch { } }
    }

    public partial class App : Application
    {
        [STAThread]
        public static void Main()
        {
            var app = new App();
            app.Run(new MainWindow());
        }
    }
}
