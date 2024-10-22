using CefSharp;
using CefSharp.Handler;
using CefSharp.Wpf;
using MaterialDesignThemes.Wpf;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Serilog;
using Streamster.ClientCore.Models.Chats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Streamster.ClientApp.Win.Support
{
    public class WebBrowserController
    {
        public static readonly DependencyProperty ModelProperty =
            DependencyProperty.RegisterAttached("Model", typeof(WebBrowserAuthenticateModel), typeof(WebBrowserController), new FrameworkPropertyMetadata(null
                                                    , OnModelChanged));

        public static int CoreWebView2WebResourceResponseReceived { get; private set; }

        public static WebBrowserAuthenticateModel GetModel(DependencyObject target) => (WebBrowserAuthenticateModel)target.GetValue(ModelProperty);

        public static void SetModel(DependencyObject target, WebBrowserAuthenticateModel value) => target.SetValue(ModelProperty, value);

        private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is WebBrowserAuthenticateModel model &&
                model.LoginSearch != null)
            {
                try
                {
                    if (d is ChromiumWebBrowser cef)
                    {
                        cef.IsBrowserInitializedChanged += Cef_IsBrowserInitializedChanged;
                        cef.Address = model.LoginSearch.StartUrl;
                        cef.RequestHandler = new InterceptRequestHandler(model);

                        model.OpenEvent += (s, e) =>
                        {
                            if (cef.IsBrowserInitialized)
                            {
                                cef.GetCookieManager().DeleteCookies();
                                cef.Address = model.LoginSearch.StartUrl;
                            }
                        };
                    }

                    if (d is WebView2 webView2)
                    {
                        webView2.CoreWebView2InitializationCompleted += (s, e) => WebViewCoreWebView2InitializationCompleted(webView2, model);
                        CoreWebView2Environment environment = CoreWebView2Environment.CreateAsync(null, model.UserDataPath, null).Result;
                        webView2.EnsureCoreWebView2Async(environment).GetAwaiter();
                        webView2.Source = new Uri(model.LoginSearch.StartUrl);

                        model.OpenEvent += (s, e) =>
                        {
                            if (webView2.CoreWebView2 != null)
                            {
                                webView2.CoreWebView2.Profile.ClearBrowsingDataAsync().GetAwaiter();
                                webView2.CoreWebView2.CookieManager.DeleteAllCookies();
                                webView2.Source = new Uri(model.LoginSearch.StartUrl);
                            }
                        };

                        model.CloseEvent += (s, e) =>
                        {
                            if (model.ShowWebView2 && webView2.CoreWebView2 != null)
                            {
                                webView2.Source = new Uri("about:blank");
                            }
                        };
                    }

                    if (d is Button button)
                    {
                        model.CloseEvent += (s, e) => button.Dispatcher.BeginInvoke(() =>
                        {
                            DialogHost.CloseDialogCommand.Execute(null, button);
                            model.NotifyParent();
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to init WebBrowserController");
                }
            }
        }

        private static void Cef_IsBrowserInitializedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ((ChromiumWebBrowser)sender).GetCookieManager().DeleteCookies();
        }

        private static void WebViewCoreWebView2InitializationCompleted(WebView2 webView2, WebBrowserAuthenticateModel model)
        {
            webView2.CoreWebView2.CookieManager.DeleteAllCookies();
            webView2.CoreWebView2.Profile.ClearBrowsingDataAsync().GetAwaiter();
            webView2.CoreWebView2.WebResourceResponseReceived += (s, e) => WebResourceResponseReceived(model, e);
        }

        private static void WebResourceResponseReceived(WebBrowserAuthenticateModel model, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            var url = e.Request.Uri.ToLower();
            var method = e.Request.Method?.ToLower(); 

            Log.Information($"Browser WebVeiw2: {url} {method}");
            var toUrl = model.LoginSearch.Entries.Where(s => UrlEqual(s.Url, url)).ToList();
            if (toUrl.Count > 0)
            {
                if (toUrl.Any(s => s.header == null)) // dependent on content
                {
                    try
                    {
                        var result = e.Response.GetContentAsync().GetAwaiter();
                        result.OnCompleted(() =>
                        {
                            try
                            {
                                var res = result.GetResult();
                                StreamReader reader = new StreamReader(res);
                                var content = reader.ReadToEnd() ?? "";

                                ProcessResource(url, method, content, header => GetHeaders(e.Response.Headers.GetHeaders(header)).ToArray(), model);
                            }
                            catch (Exception ex2)
                            {
                                Log.Information(ex2, "Failed in WebView2 OnCompleted");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Information(ex, "Failed2 in WebView2 OnCompleted");
                        ProcessResource(url, method, content: "", header => GetHeaders(e.Response.Headers.GetHeaders(header)).ToArray(), model);
                    }
                }
                else
                    ProcessResource(url, method, content: "", header => GetHeaders(e.Response.Headers.GetHeaders(header)).ToArray(), model);
            }
        }

        public static bool UrlEqual(string url1, string url2)
        {
            url1 = url1.TrimEnd('/');
            url2 = url2.TrimEnd('/');
            Uri uri1 = new Uri(url1);
            Uri uri2 = new Uri(url2);

            if (url1.EndsWith("*"))
                return url2.ToLowerInvariant().StartsWith(url1.Substring(0, url1.Length - 1).ToLowerInvariant());
            else
                return Uri.Compare(uri1, uri2,
                    UriComponents.Host | UriComponents.PathAndQuery,
                    UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static IEnumerable<string> GetHeaders(CoreWebView2HttpHeadersCollectionIterator iter)
        {
            while (iter.MoveNext())
            {
                yield return iter.Current.Value;
            };
        }

        public static bool ProcessResource(string url, string method, string content, Func<string, string[]> getHeaders, WebBrowserAuthenticateModel model)
        {
            Log.Information($"Browser WebVeiw2: Processing {url} {method} withContent: {content.Substring(0, Math.Min(content.Length, 80))}");
            foreach (var entry in model.LoginSearch.Entries)
            {
                if (UrlEqual(entry.Url, url) && method == entry.Method)
                {
                    var regex = new Regex(entry.regex);

                    string[] sources = null;
                    if (entry.header != null)
                    {
                        sources = getHeaders(entry.header);
                        Log.Information($"Browser WebVeiw2: Processing {url}, Headers[{entry.header}] = " + 
                            sources == null ? "<null>" : string.Join(", ", sources));
                    }
                    else
                        sources = new[] { content };

                    if (sources != null)
                    {
                        foreach (var source in sources)
                        {
                            var matches = regex.Matches(source);
                            foreach (Match match in matches)
                                if (match.Success)
                                    if (model.AddValue(entry.parameter, match.Value))
                                        return true;
                        }
                    }
                }
            }
            return false;
        }
    }

    public class InterceptRequestHandler : RequestHandler
    {
        private WebBrowserAuthenticateModel _model;

        public InterceptRequestHandler(WebBrowserAuthenticateModel model)
        {
            _model = model;
        }

        protected override IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            var method = request.Method?.ToLower();
            var url = request.Url?.ToLower();

            Log.Information($"Browser CEF: GetResourceRequestHandler: {url} {method}");

            if (_model.LoginSearch.Entries.Any(s => WebBrowserController.UrlEqual(s.Url, url)))
                return new InterceptResourceRequestHandler(_model);

            return base.GetResourceRequestHandler(chromiumWebBrowser, browser, frame, request, isNavigation, isDownload, requestInitiator, ref disableDefaultHandling);
        }
    }

    public class InterceptResourceRequestHandler : ResourceRequestHandler
    {
        private InterceptResponseFilter _filter = new InterceptResponseFilter();
        private WebBrowserAuthenticateModel _model;

        public InterceptResourceRequestHandler(WebBrowserAuthenticateModel model)
        {
            _model = model;
        }

        protected override void OnResourceRedirect(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl)
        {
            var (url, method) = GetRequestDetails(request);
            Log.Information($"Browser CEF: OnResourceRedirect: {url} {method}");

            WebBrowserController.ProcessResource(url, method, content: "", headerName => response.Headers.GetValues(headerName), _model);

            base.OnResourceRedirect(chromiumWebBrowser, browser, frame, request, response, ref newUrl);
        }

        private (string url, string method) GetRequestDetails(IRequest request)
        {
            return (request.Url?.ToLower(), request.Method?.ToLower());
        }

        protected override IResponseFilter GetResourceResponseFilter(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
        {
            var (url, method) = GetRequestDetails(request);
            Log.Information($"Browser CEF: GetResourceResponseFilter: {url} {method}");

            if (_model.LoginSearch.Entries.Any(s => WebBrowserController.UrlEqual(s.Url, url)))
                return _filter;

            return base.GetResourceResponseFilter(chromiumWebBrowser, browser, frame, request, response);
        }

        protected override void OnResourceLoadComplete(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
        {
            var (url, method) = GetRequestDetails(request);
            var content = _filter.Data ?? "";

            Log.Information($"Browser CEF: OnResourceLoadComplete: {url} {method} : {content.Substring(0, Math.Min(content.Length, 80))}");
            WebBrowserController.ProcessResource(url, method, content, headerName => response.Headers.GetValues(headerName), _model);
            
            base.OnResourceLoadComplete(chromiumWebBrowser, browser, frame, request, response, status, receivedContentLength);
        }

        protected override bool OnResourceResponse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
        {
            var (url, method) = GetRequestDetails(request);
            Log.Information($"Browser CEF: OnResourceResponse: {url} {method}");
            return base.OnResourceResponse(chromiumWebBrowser, browser, frame, request, response);
        }
    }

    public class InterceptResponseFilter : IResponseFilter
    {
        MemoryStream _memoryStream = new MemoryStream();

        bool IResponseFilter.InitFilter()
        {
            return true;
        }

        FilterStatus IResponseFilter.Filter(Stream dataIn, out long dataInRead, Stream dataOut, out long dataOutWritten)
        {
            if (dataIn == null)
            {
                dataInRead = 0;
                dataOutWritten = 0;

                return FilterStatus.Done;
            }

            dataInRead = Math.Min(dataIn.Length, dataOut.Length);
            dataOutWritten = dataInRead;

            var readBytes = new byte[dataInRead];
            dataIn.Read(readBytes, 0, readBytes.Length);
            dataOut.Write(readBytes, 0, readBytes.Length);
            _memoryStream.Write(readBytes, 0, readBytes.Length);

            if (dataInRead < dataIn.Length)
                return FilterStatus.NeedMoreData;

            return FilterStatus.Done;
        }

        public void Dispose()
        {
        }

        public string Data
        {
            get { return Encoding.UTF8.GetString(_memoryStream.ToArray()); }
        }
    }
}
