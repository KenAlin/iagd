﻿using System;
using System.IO;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using IAGrim.Utilities;
using log4net;

namespace IAGrim.UI.Misc.CEF {
    public class CefBrowserHandler : IUserFeedbackHandler, IDisposable {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CefBrowserHandler));
        private ChromiumWebBrowser _browser;
        public event EventHandler TransferSingleRequested;
        public event EventHandler TransferAllRequested;

        public ChromiumWebBrowser BrowserControl => _browser;

        private readonly object _lockObj = new object();

        ~CefBrowserHandler() {
            Dispose();
        }
        public void Dispose() {
            lock (_lockObj) {
                if (_browser != null) {
                    CefSharpSettings.WcfTimeout = TimeSpan.Zero;
                    _browser.Dispose();

                    Cef.Shutdown();
                    _browser = null;
                }
            }
        }

        public void ShowDevTools() {
            _browser.ShowDevTools();
        }

        public void ShowLoadingAnimation() {
            if (_browser.IsBrowserInitialized) {
                _browser.ExecuteScriptAsync("data.globalStore.dispatch(data.globalSetIsLoading(true));");
            }
        }

        public void RefreshItems() {
            if (_browser.IsBrowserInitialized) {
                _browser.ExecuteScriptAsync("data.globalStore.dispatch(data.globalSetItems(JSON.parse(data.Items)));");
            }
            else {
                Logger.Warn("Attempted to update items but CEF not yet initialized.");
            }
        }

        public void JsCallback(string method, string json) {
            if (_browser.IsBrowserInitialized) {
                _browser.ExecuteScriptAsync($"{method}({json});");
            }
            else {
                Logger.Warn("Attempted to execute a callback but CEF not yet initialized.");
            }
        }

        public void SetRecipes(string json)
        {
            if (_browser.IsBrowserInitialized)
            {
                var command = $"{dispatch}(data.globalSetRecipes({json}));";
                _browser.ExecuteScriptAsync(command);
            }
            else
            {
                Logger.Warn("Attempted a call to JS CEF not yet initialized.");
            }
        }

        public void SetRecipeIngredients(string json)
        {
            if (_browser.IsBrowserInitialized)
            {
                var command = $"{dispatch}(data.globalSetRecipeIngredients({json}));";
                _browser.ExecuteScriptAsync(command);
            }
            else
            {
                Logger.Warn("Attempted a call to JS CEF not yet initialized.");
            }
        }

        public void AddItems() {
            if (_browser.IsBrowserInitialized) {
                _browser.ExecuteScriptAsync($"{dispatch}(data.globalAddItems(JSON.parse(data.Items)));");
            }
            else {
                Logger.Warn("Attempted to update items but CEF not yet initialized.");
            }
        }

        private const string dispatch = "data.globalStore.dispatch";
        
        public void ShowMessage(string message, UserFeedbackLevel level) {
            string levelLowercased = level.ToString().ToLower();
            var m = message.Replace("\n", "\\n").Replace("'", "\\'");
            if (!string.IsNullOrEmpty(message)) {
                if (_browser.IsBrowserInitialized)
                    _browser.ExecuteScriptAsync($"{dispatch}(data.showMessage('{m}', '{levelLowercased}'));");
                else
                    Logger.Warn($"Attempted to display message \"{message}\" but browser is not yet initialized");
            }
        }

        public void ShowMessage(string message) {
            ShowMessage(message, UserFeedbackLevel.Info);
        }

        public void InitializeChromium(object bindeable, EventHandler<IsBrowserInitializedChangedEventArgs> Browser_IsBrowserInitializedChanged) {
            try {
                Logger.Info("Creating Chromium instance..");
                Cef.EnableHighDPISupport();

                //var settings = new CefSettings();
                //settings.CefCommandLineArgs.Add("disable-gpu", "1");
                if (!Cef.Initialize()) {
                    Logger.Fatal("Could not initialize Chromium");
                    MessageBox.Show("Fatal error - Could not initialize chromium", "Fatal error", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                _browser = new ChromiumWebBrowser(GlobalPaths.ItemsHtmlFile);
                _browser.RegisterJsObject("data", bindeable, false);
                _browser.IsBrowserInitializedChanged += Browser_IsBrowserInitializedChanged;

                var requestInterceptor = new CefRequestHandler();
                requestInterceptor.TransferSingleRequested += (sender, args) => this.TransferSingleRequested?.Invoke(sender, args);
                requestInterceptor.TransferAllRequested += (sender, args) => this.TransferAllRequested?.Invoke(sender, args);
                requestInterceptor.OnSetZoom += (sender, args) => this.BrowserControl.SetZoomLevel((args as SetZoomEvent)?.ZoomLevel ?? 1.0);

                _browser.RequestHandler = requestInterceptor;

                //browser.RequestHandler = new TransferUrlHijack { TransferMethod = transferItem };
                Logger.Info("Chromium created..");
            } catch (System.IO.FileNotFoundException ex) {
                MessageBox.Show("Error \"File Not Found\" loading Chromium, did you forget to install Visual C++ runtimes?\n\nvc_redist86 in the IA folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Logger.Warn(ex.Message);
                Logger.Warn(ex.StackTrace);
                throw;
            } catch (IOException ex) {
                MessageBox.Show("Error loading Chromium, did you forget to install Visual C++ runtimes?\n\nvc_redist86 in the IA folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Logger.Warn(ex.Message);
                Logger.Warn(ex.StackTrace);
                throw;
            } catch (Exception ex) {
                MessageBox.Show("Unknown error loading Chromium, please see log file for more information.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Logger.Warn(ex.Message);
                Logger.Warn(ex.StackTrace);
                throw;
            }
        }
    }
}
