using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ECode.ViewModels;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;

namespace ECode.Controls;

/// <summary>
/// 基于 WebView2 的应用内浏览器控件。提供工具栏（后退/前进/刷新/地址栏），
/// 以及供脚本与网页交互的可编程 API。
/// </summary>
public partial class BrowserControl : UserControl
{
    public event Action? CloseRequested;

    public BrowserPaneViewModel Browser { get; } = new();

    public BrowserControl()
    {
        InitializeComponent();
        Browser.PropertyChanged += (_, _) => UpdateToolbarState();
        UpdateToolbarState();
        InitializeWebView();
    }

    private async void InitializeWebView()
    {
        try
        {
            await WebView.EnsureCoreWebView2Async();
            HideErrorOverlay();
            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            WebView.CoreWebView2.DocumentTitleChanged += (_, _) =>
            {
                Browser.UpdateNavigationState(
                    WebView.CoreWebView2.Source,
                    WebView.CoreWebView2.DocumentTitle,
                    WebView.CoreWebView2.CanGoBack,
                    WebView.CoreWebView2.CanGoForward);
            };
            WebView.CoreWebView2.HistoryChanged += (_, _) => SyncBrowserStateFromWebView();
            SyncBrowserStateFromWebView();
        }
        catch (Exception ex)
        {
            ShowWebViewError("请安装或修复 Microsoft Edge WebView2 Runtime 后重试。\n\n" + ex.Message);
            Debug.WriteLine($"WebView2 init failed: {ex.Message}");
        }
    }

    /// <summary>导航到指定 URL。</summary>
    public void Navigate(string url)
    {
        url = BrowserPaneViewModel.NormalizeUrl(url);
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Browser.BeginNavigation(url);
            if (WebView.CoreWebView2 != null)
                WebView.CoreWebView2.Navigate(url);
            else
                WebView.Source = new Uri(url);

            AddressBar.Text = url;
        }
        catch (Exception ex)
        {
            Browser.CompleteNavigation(url, Browser.Title, Browser.CanGoBack, Browser.CanGoForward, success: false, ex.Message);
        }
    }

    /// <summary>执行 JavaScript 并返回结果。</summary>
    public async Task<string> EvaluateJavaScript(string script)
    {
        if (WebView.CoreWebView2 == null) return "";
        return await WebView.CoreWebView2.ExecuteScriptAsync(script);
    }

    /// <summary>获取无障碍树快照（简化版）。</summary>
    public async Task<string> GetAccessibilitySnapshot()
    {
        const string script = @"
            (function() {
                function walk(node) {
                    const result = {
                        role: node.getAttribute('role') || node.tagName.toLowerCase(),
                        name: node.getAttribute('aria-label') || node.textContent?.substring(0, 100) || '',
                        children: []
                    };
                    for (const child of node.children) {
                        result.children.push(walk(child));
                    }
                    return result;
                }
                return JSON.stringify(walk(document.body));
            })()
        ";
        return await EvaluateJavaScript(script);
    }

    /// <summary>通过 CSS 选择器点击元素。</summary>
    public async Task ClickElement(string selector)
    {
        var escapedSelector = selector.Replace("'", "\\'");
        await EvaluateJavaScript($"document.querySelector('{escapedSelector}')?.click()");
    }

    /// <summary>通过 CSS 选择器填充表单字段。</summary>
    public async Task FillElement(string selector, string value)
    {
        var escapedSelector = selector.Replace("'", "\\'");
        var escapedValue = value.Replace("'", "\\'");
        await EvaluateJavaScript($@"
            (() => {{
                const el = document.querySelector('{escapedSelector}');
                if (el) {{
                    el.value = '{escapedValue}';
                    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                }}
            }})()
        ");
    }

    /// <summary>获取当前页面 URL。</summary>
    public string GetCurrentUrl()
    {
        return WebView.CoreWebView2?.Source ?? "";
    }

    // 事件处理器

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2?.CanGoBack == true)
            WebView.CoreWebView2.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2?.CanGoForward == true)
            WebView.CoreWebView2.GoForward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        WebView.CoreWebView2?.Reload();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        WebView.CoreWebView2?.Stop();
        Browser.CompleteNavigation(
            WebView.CoreWebView2?.Source ?? WebView.Source?.ToString(),
            WebView.CoreWebView2?.DocumentTitle,
            WebView.CoreWebView2?.CanGoBack == true,
            WebView.CoreWebView2?.CanGoForward == true,
            success: true);
    }

    private void DevTools_Click(object sender, RoutedEventArgs e)
    {
        WebView.CoreWebView2?.OpenDevToolsWindow();
    }

    private void DownloadWebView2_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ShowWebViewError("无法打开下载页面，请手动访问 https://developer.microsoft.com/microsoft-edge/webview2/\n\n" + ex.Message);
        }
    }

    private void CloseBrowser_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private void AddressBar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Navigate(AddressBar.Text);
            e.Handled = true;
        }
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        Browser.CompleteNavigation(
            WebView.CoreWebView2?.Source ?? WebView.Source?.ToString(),
            WebView.CoreWebView2?.DocumentTitle,
            WebView.CoreWebView2?.CanGoBack == true,
            WebView.CoreWebView2?.CanGoForward == true,
            e.IsSuccess,
            e.WebErrorStatus.ToString());
        SyncAddressBarFromBrowser();
    }

    private void WebView_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        SyncBrowserStateFromWebView();
        SyncAddressBarFromBrowser();
    }

    private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        Browser.BeginNavigation(e.Uri);
        SyncAddressBarFromBrowser();
    }

    private void SyncBrowserStateFromWebView()
    {
        Browser.UpdateNavigationState(
            WebView.CoreWebView2?.Source ?? WebView.Source?.ToString(),
            WebView.CoreWebView2?.DocumentTitle,
            WebView.CoreWebView2?.CanGoBack == true,
            WebView.CoreWebView2?.CanGoForward == true);
    }

    private void SyncAddressBarFromBrowser()
    {
        if (!AddressBar.IsKeyboardFocusWithin)
            AddressBar.Text = Browser.Url;
    }

    private void UpdateToolbarState()
    {
        if (!IsInitialized)
            return;

        BackButton.IsEnabled = Browser.IsWebViewAvailable && Browser.CanGoBack;
        ForwardButton.IsEnabled = Browser.IsWebViewAvailable && Browser.CanGoForward;
        ReloadButton.IsEnabled = Browser.IsWebViewAvailable && !Browser.IsLoading;
        StopButton.IsEnabled = Browser.IsWebViewAvailable && Browser.IsLoading;
        StopButton.Visibility = Browser.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        DevToolsButton.IsEnabled = Browser.IsWebViewAvailable && WebView.CoreWebView2 != null;
        LoadingProgress.Visibility = Browser.IsLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowWebViewError(string message)
    {
        Browser.SetWebViewUnavailable(message);
        ErrorMessageText.Text = message;
        ErrorOverlay.Visibility = Visibility.Visible;
        WebView.Visibility = Visibility.Collapsed;
    }

    private void HideErrorOverlay()
    {
        ErrorOverlay.Visibility = Visibility.Collapsed;
        WebView.Visibility = Visibility.Visible;
    }
}

