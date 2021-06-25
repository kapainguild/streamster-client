// Copyright © 2014 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using System;
using System.Threading;
using CefSharp;
using CefSharp.Enums;
using CefSharp.Internals;
using CefSharp.Structs;
using Range = CefSharp.Structs.Range;
using Size = System.Drawing.Size;

namespace DynamicStreamerCef
{
    /// <summary>
    /// An offscreen instance of Chromium that you can use to take
    /// snapshots or evaluate JavaScript.
    /// </summary>
    public partial class ChromiumWebBrowser : IRenderWebBrowser
    {
        /// <summary>
        /// The managed cef browser adapter
        /// </summary>
        private IBrowserAdapter managedCefBrowserAdapter;

        /// <summary>
        /// The browser
        /// </summary>
        private IBrowser browser;

        /// <summary>
        /// Flag to guard the creation of the underlying offscreen browser - only one instance can be created
        /// </summary>
        private bool browserCreated;

        /// <summary>
        /// The value for disposal, if it's 1 (one) then this instance is either disposed
        /// or in the process of getting disposed
        /// </summary>
        private int disposeSignaled;

        private readonly IRenderTarget _renderTarget;

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value><see langword="true" /> if this instance is disposed; otherwise, <see langword="false" />.</value>
        public bool IsDisposed
        {
            get
            {
                return Interlocked.CompareExchange(ref disposeSignaled, 1, 1) == 1;
            }
        }

        /// <summary>
        /// A flag that indicates whether the WebBrowser is initialized (true) or not (false).
        /// </summary>
        /// <value><c>true</c> if this instance is browser initialized; otherwise, <c>false</c>.</value>
        public bool IsBrowserInitialized
        {
            get { return InternalIsBrowserInitialized(); }
        }
        /// <summary>
        /// A flag that indicates whether the control is currently loading one or more web pages (true) or not (false).
        /// </summary>
        /// <value><c>true</c> if this instance is loading; otherwise, <c>false</c>.</value>
        /// <remarks>In the WPF control, this property is implemented as a Dependency Property and fully supports data
        /// binding.</remarks>
        public bool IsLoading { get; private set; }
        /// <summary>
        /// The text that will be displayed as a ToolTip
        /// </summary>
        /// <value>The tooltip text.</value>
        public string TooltipText { get; private set; }
        /// <summary>
        /// The address (URL) which the browser control is currently displaying.
        /// Will automatically be updated as the user navigates to another page (e.g. by clicking on a link).
        /// </summary>
        /// <value>The address.</value>
        /// <remarks>In the WPF control, this property is implemented as a Dependency Property and fully supports data
        /// binding.</remarks>
        public string Address { get; private set; }
        /// <summary>
        /// A flag that indicates whether the state of the control current supports the GoBack action (true) or not (false).
        /// </summary>
        /// <value><c>true</c> if this instance can go back; otherwise, <c>false</c>.</value>
        /// <remarks>In the WPF control, this property is implemented as a Dependency Property and fully supports data
        /// binding.</remarks>
        public bool CanGoBack { get; private set; }
        /// <summary>
        /// A flag that indicates whether the state of the control currently supports the GoForward action (true) or not (false).
        /// </summary>
        /// <value><c>true</c> if this instance can go forward; otherwise, <c>false</c>.</value>
        /// <remarks>In the WPF control, this property is implemented as a Dependency Property and fully supports data
        /// binding.</remarks>
        public bool CanGoForward { get; private set; }
        /// <summary>
        /// Gets the request context.
        /// </summary>
        /// <value>The request context.</value>
        public IRequestContext RequestContext { get; private set; }
        /// <summary>
        /// Implement <see cref="IAccessibilityHandler" /> to handle events related to accessibility.
        /// </summary>
        /// <value>The accessibility handler.</value>
        public IAccessibilityHandler AccessibilityHandler { get; set; }
        /// <summary>
        /// Event called after the underlying CEF browser instance has been created. 
        /// It's important to note this event is fired on a CEF UI thread, which by default is not the same as your application UI
        /// thread. It is unwise to block on this thread for any length of time as your browser will become unresponsive and/or hang..
        /// To access UI elements you'll need to Invoke/Dispatch onto the UI Thread.
        /// (The exception to this is when you're running with settings.MultiThreadedMessageLoop = false, then they'll be the same thread).
        /// </summary>
        public event EventHandler BrowserInitialized;
        /// <summary>
        /// Occurs when the browser address changed.
        /// It's important to note this event is fired on a CEF UI thread, which by default is not the same as your application UI
        /// thread. It is unwise to block on this thread for any length of time as your browser will become unresponsive and/or hang..
        /// To access UI elements you'll need to Invoke/Dispatch onto the UI Thread.
        /// (The exception to this is when you're running with settings.MultiThreadedMessageLoop = false, then they'll be the same thread).
        /// </summary>
        public event EventHandler<AddressChangedEventArgs> AddressChanged;
        /// <summary>
        /// Occurs when [title changed].
        /// It's important to note this event is fired on a CEF UI thread, which by default is not the same as your application UI
        /// thread. It is unwise to block on this thread for any length of time as your browser will become unresponsive and/or hang..
        /// To access UI elements you'll need to Invoke/Dispatch onto the UI Thread.
        /// (The exception to this is when you're running with settings.MultiThreadedMessageLoop = false, then they'll be the same thread).
        /// </summary>
        public event EventHandler<TitleChangedEventArgs> TitleChanged;

        /// <summary>
        /// Fired on the CEF UI thread, which by default is not the same as your application main thread.
        /// Called when an element should be painted. Pixel values passed to this method are scaled relative to view coordinates
        /// based on the value of ScreenInfo.DeviceScaleFactor returned from GetScreenInfo. 
        /// </summary>
        public event EventHandler<OnPaintEventArgs> Paint;

        /// <summary>
        /// Create a new OffScreen Chromium Browser. If you use <see cref="CefSharp.JavascriptBinding.JavascriptBindingSettings.LegacyBindingEnabled"/> = true then you must
        /// set <paramref name="automaticallyCreateBrowser"/> to false and call <see cref="CreateBrowser"/> after the objects are registered.
        /// </summary>
        /// <param name="address">Initial address (url) to load</param>
        /// <param name="browserSettings">The browser settings to use. If null, the default settings are used.</param>
        /// <param name="requestContext">See <see cref="RequestContext" /> for more details. Defaults to null</param>
        /// <param name="automaticallyCreateBrowser">automatically create the underlying Browser</param>
        /// <exception cref="System.InvalidOperationException">Cef::Initialize() failed</exception>
        public ChromiumWebBrowser(string address, IBrowserSettings browserSettings, IRequestContext requestContext, bool automaticallyCreateBrowser, IRenderTarget renderer)
        {
            if (!Cef.IsInitialized)
            {
                throw new InvalidOperationException("Cef IS NOT INITIALIZED");
            }

            RequestContext = requestContext;
            _renderTarget = renderer;
            Cef.AddDisposable(this);
            Address = address;

            managedCefBrowserAdapter = ManagedCefBrowserAdapter.Create(this, true);

            if (automaticallyCreateBrowser)
            {
                CreateBrowser(null, browserSettings);
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ChromiumWebBrowser"/> class.
        /// </summary>
        ~ChromiumWebBrowser()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all resources used by the <see cref="ChromiumWebBrowser"/> object
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources for the <see cref="ChromiumWebBrowser"/>
        /// </summary>
        /// <param name="disposing"><see langword="true" /> to release both managed and unmanaged resources; <see langword="false" /> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            // Attempt to move the disposeSignaled state from 0 to 1. If successful, we can be assured that
            // this thread is the first thread to do so, and can safely dispose of the object.
            if (Interlocked.CompareExchange(ref disposeSignaled, 1, 0) != 0)
            {
                return;
            }

            if (disposing)
            {
                CanExecuteJavascriptInMainFrame = false;
                Interlocked.Exchange(ref browserInitialized, 0);

                // Don't reference event listeners any longer:
                AddressChanged = null;
                BrowserInitialized = null;
                ConsoleMessage = null;
                FrameLoadEnd = null;
                FrameLoadStart = null;
                LoadError = null;
                LoadingStateChanged = null;
                Paint = null;
                StatusMessage = null;
                TitleChanged = null;
                JavascriptMessageReceived = null;

                // Release reference to handlers, except LifeSpanHandler which is done after Disposing
                // ManagedCefBrowserAdapter otherwise the ILifeSpanHandler.DoClose will not be invoked.
                this.SetHandlersToNullExceptLifeSpan();

                browser = null;

                if (managedCefBrowserAdapter != null)
                {
                    managedCefBrowserAdapter.Dispose();
                    managedCefBrowserAdapter = null;
                }

                // LifeSpanHandler is set to null after managedCefBrowserAdapter.Dispose so ILifeSpanHandler.DoClose
                // is called.
                LifeSpanHandler = null;
            }

            Cef.RemoveDisposable(this);
        }

        /// <summary>
        /// Create the underlying browser. The instance address, browser settings and request context will be used.
        /// </summary>
        /// <param name="windowInfo">Window information used when creating the browser</param>
        /// <param name="browserSettings">Browser initialization settings</param>
        /// <exception cref="System.Exception">An instance of the underlying offscreen browser has already been created, this method can only be called once.</exception>
        public void CreateBrowser(IWindowInfo windowInfo = null, IBrowserSettings browserSettings = null)
        {
            if (browserCreated)
            {
                throw new Exception("An instance of the underlying offscreen browser has already been created, this method can only be called once.");
            }

            browserCreated = true;

            if (browserSettings == null)
            {
                browserSettings = CefSharp.Core.ObjectFactory.CreateBrowserSettings(autoDispose: true);
            }

            if (windowInfo == null)
            {
                windowInfo = CefSharp.Core.ObjectFactory.CreateWindowInfo();
                windowInfo.SetAsWindowless(IntPtr.Zero);
            }

            managedCefBrowserAdapter.CreateBrowser(windowInfo, browserSettings, RequestContext, Address);

            //Dispose of BrowserSettings if we created it, if user created then they're responsible
            if (browserSettings.AutoDispose)
            {
                browserSettings.Dispose();
            }
            browserSettings = null;
        }

        /// <summary>
        /// Get/set the size of the Chromium viewport, in pixels.
        /// This also changes the size of the next rendered bitmap.
        /// </summary>
        /// <value>The size.</value>
        public Size Size
        {
            get { return _renderTarget.Size; }
            set
            {
                /*if (size != value)
                {
                    size = value;

                    if (IsBrowserInitialized)
                    {
                        browser.GetHost().WasResized();
                    }
                }*/
            }
        }

        /// <summary>
        /// Loads the specified URL.
        /// </summary>
        /// <param name="url">The URL to be loaded.</param>
        public void Load(string url)
        {
            Address = url;

            //Destroy the frame wrapper when we're done
            using (var frame = this.GetMainFrame())
            {
                frame.LoadUrl(url);
            }
        }

        /// <summary>
        /// The javascript object repository, one repository per ChromiumWebBrowser instance.
        /// </summary>
        public IJavascriptObjectRepository JavascriptObjectRepository
        {
            get { return managedCefBrowserAdapter?.JavascriptObjectRepository; }
        }

        /// <summary>
        /// Has Focus - Always False
        /// </summary>
        /// <returns>returns false</returns>
        bool IWebBrowser.Focus()
        {
            // no control to focus for offscreen browser
            return false;
        }

        /// <summary>
        /// Returns the current CEF Browser Instance
        /// </summary>
        /// <returns>browser instance or null</returns>
        public IBrowser GetBrowser()
        {
            ThrowExceptionIfDisposed();
            ThrowExceptionIfBrowserNotInitialized();

            return browser;
        }

        /// <summary>
        /// Gets the screen information (scale factor).
        /// </summary>
        /// <returns>ScreenInfo.</returns>
        ScreenInfo? IRenderWebBrowser.GetScreenInfo()
        {
            var screenInfo = new ScreenInfo { DeviceScaleFactor = 1.0F };

            return screenInfo;
        }

        /// <summary>
        /// Gets the view rect (width, height)
        /// </summary>
        /// <returns>ViewRect.</returns>
        Rect IRenderWebBrowser.GetViewRect()
        {
            var viewRect = new Rect(0, 0, Size.Width, Size.Height);

            return viewRect;
        }

        /// <summary>
        /// Called to retrieve the translation from view coordinates to actual screen coordinates. 
        /// </summary>
        /// <param name="viewX">x</param>
        /// <param name="viewY">y</param>
        /// <param name="screenX">screen x</param>
        /// <param name="screenY">screen y</param>
        /// <returns>Return true if the screen coordinates were provided.</returns>
        bool IRenderWebBrowser.GetScreenPoint(int viewX, int viewY, out int screenX, out int screenY)
        {
            screenX = viewX;
            screenY = viewY;

            return false;
        }

         /// <summary>
        /// Called when an element has been rendered to the shared texture handle.
        /// This method is only called when <see cref="IWindowInfo.SharedTextureEnabled"/> is set to true
        /// </summary>
        /// <param name="type">indicates whether the element is the view or the popup widget.</param>
        /// <param name="dirtyRect">contains the set of rectangles in pixel coordinates that need to be repainted</param>
        /// <param name="sharedHandle">is the handle for a D3D11 Texture2D that can be accessed via ID3D11Device using the OpenSharedResource method.</param>
        void IRenderWebBrowser.OnAcceleratedPaint(PaintElementType type, Rect dirtyRect, IntPtr sharedHandle)
        {
        }

        /// <summary>
        /// Called when an element should be painted. (Invoked from CefRenderHandler.OnPaint)
        /// </summary>
        /// <param name="type">indicates whether the element is the view or the popup widget.</param>
        /// <param name="dirtyRect">contains the set of rectangles in pixel coordinates that need to be repainted</param>
        /// <param name="buffer">The bitmap will be will be  width * height *4 bytes in size and represents a BGRA image with an upper-left origin</param>
        /// <param name="width">width</param>
        /// <param name="height">height</param>
        void IRenderWebBrowser.OnPaint(PaintElementType type, Rect dirtyRect, IntPtr buffer, int width, int height)
        {
            var handled = false;

            var args = new OnPaintEventArgs(type == PaintElementType.Popup, dirtyRect, buffer, width, height);

            var handler = Paint;
            if (handler != null)
            {
                handler(this, args);
                handled = args.Handled;
            }

            if (!handled)
                _renderTarget.OnPaint(type == PaintElementType.View, buffer, width, height);
        }

        /// <summary>
        /// Called when the browser's cursor has changed. . 
        /// </summary>
        /// <param name="cursor">If type is Custom then customCursorInfo will be populated with the custom cursor information</param>
        /// <param name="type">cursor type</param>
        /// <param name="customCursorInfo">custom cursor Information</param>
        void IRenderWebBrowser.OnCursorChange(IntPtr cursor, CursorType type, CursorInfo customCursorInfo)
        {
        }

        /// <summary>
        /// Starts dragging.
        /// </summary>
        /// <param name="dragData">The drag data.</param>
        /// <param name="mask">The mask.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool IRenderWebBrowser.StartDragging(IDragData dragData, DragOperationsMask mask, int x, int y)
        {
            return false;
        }

        void IRenderWebBrowser.UpdateDragCursor(DragOperationsMask operation)
        {
        }

        /// <summary>
        /// Sets the popup is open.
        /// </summary>
        /// <param name="show">if set to <c>true</c> [show].</param>
        void IRenderWebBrowser.OnPopupShow(bool show)
        {
           _renderTarget.OnPopupShow(show);
        }

        /// <summary>
        /// Called when the browser wants to move or resize the popup widget. 
        /// </summary>
        /// <param name="rect">contains the new location and size in view coordinates. </param>
        void IRenderWebBrowser.OnPopupSize(Rect rect)
        {
            _renderTarget.OnPopupSize(rect.X, rect.Y, rect.Width, rect.Height);
        }

        void IRenderWebBrowser.OnImeCompositionRangeChanged(Range selectedRange, Rect[] characterBounds)
        {
        }

        void IRenderWebBrowser.OnVirtualKeyboardRequested(IBrowser browser, TextInputMode inputMode)
        {
        }

        /// <summary>
        /// Called when [after browser created].
        /// </summary>
        /// <param name="browser">The browser.</param>
        void IWebBrowserInternal.OnAfterBrowserCreated(IBrowser browser)
        {
            this.browser = browser;

            Interlocked.Exchange(ref browserInitialized, 1);

            BrowserInitialized?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sets the address.
        /// </summary>
        /// <param name="args">The <see cref="AddressChangedEventArgs"/> instance containing the event data.</param>
        void IWebBrowserInternal.SetAddress(AddressChangedEventArgs args)
        {
            Address = args.Address;

            AddressChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Sets the loading state change.
        /// </summary>
        /// <param name="args">The <see cref="LoadingStateChangedEventArgs"/> instance containing the event data.</param>
        void IWebBrowserInternal.SetLoadingStateChange(LoadingStateChangedEventArgs args)
        {
            CanGoBack = args.CanGoBack;
            CanGoForward = args.CanGoForward;
            IsLoading = args.IsLoading;

            LoadingStateChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Sets the title.
        /// </summary>
        /// <param name="args">The <see cref="TitleChangedEventArgs"/> instance containing the event data.</param>
        void IWebBrowserInternal.SetTitle(TitleChangedEventArgs args)
        {
            TitleChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Sets the tooltip text.
        /// </summary>
        /// <param name="tooltipText">The tooltip text.</param>
        void IWebBrowserInternal.SetTooltipText(string tooltipText)
        {
            TooltipText = tooltipText;
        }
    }
}
