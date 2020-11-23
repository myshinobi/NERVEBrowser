using CefSharp;
using CefSharp.Handler;
using CefSharp.OffScreen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace NERVEBrowser
{
    public class Headless
    {
        private string ProxyHost;
        private string ProxyPort;
        private string ProxyUsername;
        private string ProxyPassword;
        private string _UserAgent;
        public bool SaveTouchScreenshots;
        public DirectoryInfo ScreenshotsPath;
        public string[] ForceUserAgentOnURLs;

        public string UserAgent
        {
            get
            {
                return _UserAgent;
            }
            set
            {
                _UserAgent = value;
                UpdateUserAgent();
            }
        }
        private System.Net.WebProxy WebProxy;
        private static string IpCheckURL = @"https://mysocial.ninja/api/userAgent";
        public bool isLoading;
        public ChromiumWebBrowser chrome;
        private RequestContext requestContext;
        private BrowserSettings browserSettings;
        public DirectoryInfo cachePath;
        private RequestContextSettings requestContextSettings;
        public static bool Initialized = false;
        public bool BrowserInitialized { get; private set; } = false;
        public bool BrowserFailed { get; private set; } = false;
        public string BrowserFailedMessage { get; set; }
        private ICookieManager CookieManager;
        private IBrowser Browser;
        private IBrowserHost Host;
        private Thread LerpMouseThread;
        private bool FingerLifted = true;
        private bool _fingerTouching = false;
        private PointF _mousePosition;
        private PointF _LerpMousePosition;
        private float _LerpMouseTime;
        private PointF _LerpStart;
        public int PageLoadBufferTimeSeconds = 1000 * 5;
        public string currentLoadedUserAgent { get; private set; }
        public string Title { get; private set; }

        public bool MouseMoving
        {
            get
            {
                return (_LerpMousePosition.Equals(_mousePosition) == false);
            }
        }
        public PointF MousePosition
        {
            get
            {
                return _mousePosition;
            }
            set
            {

                if (value.Equals(_mousePosition) == false)
                {
                    _LerpMouseTime = 0;
                    _LerpStart = _LerpMousePosition;

                }
                //_mousePosition = WiggleMouse(value,4);
                _mousePosition = value;
                if (FingerLifted)
                {
                    _LerpMousePosition = _mousePosition;
                }
            }
        }
        PointF Lerp(PointF firstVector, PointF secondVector, float time)
        {
            float retX = Lerp(firstVector.X, secondVector.X, time);
            float retY = Lerp(firstVector.Y, secondVector.Y, time);
            return new PointF(retX, retY);
        }
        float Lerp(float firstFloat, float secondFloat, float time)
        {
            return firstFloat * (1 - time) + secondFloat * time;
        }
        private Random random = new Random();
        private PointF WiggleMouse(PointF mouse, float amount)
        {
            double wiggleX = ((random.NextDouble() * 2) - 1);
            double wiggleY = ((random.NextDouble() * 2) - 1);
            mouse.X += (float)(amount * wiggleX);
            mouse.Y += (float)(amount * wiggleY);

            return mouse;
        }
        public enum Direction
        {
            Up = 0,
            Down = 1,
            Left = 2,
            Right = 3,
            None = 4
        }

        public void Swipe(Direction direction)
        {
            if (direction == Direction.None)
                return;
            Swipe(direction, new PointF(chrome.Size.Width / 2, chrome.Size.Height / 2));
        }

        public void Swipe(Direction direction, PointF goal)
        {
            if (direction == Direction.None)
                return;
            int xy = 0;
            switch (direction)
            {
                case Direction.Up:
                    xy = (int)goal.X;
                    if (xy >= chrome.Size.Width || xy <= 0)
                    {
                        xy = chrome.Size.Width / 2;
                    }
                    break;
                case Direction.Down:
                    xy = (int)goal.X;
                    if (xy >= chrome.Size.Width || xy <= 0)
                    {
                        xy = chrome.Size.Width / 2;
                    }
                    break;
                case Direction.Left:
                    xy = (int)goal.Y;
                    if (xy >= chrome.Size.Height || xy <= 0)
                    {
                        xy = chrome.Size.Height / 2;
                    }
                    break;
                case Direction.Right:
                    xy = (int)goal.Y;
                    if (xy >= chrome.Size.Height || xy <= 0)
                    {
                        xy = chrome.Size.Height / 2;
                    }
                    break;
                default:
                    break;
            }
            Swipe(xy, direction);
        }
        public void Swipe(int xy, Direction direction)
        {
            if (direction == Direction.None)
                return;
            Point start = new Point();
            Point goal = new Point();
            int margin = 0;//((int)(random.NextDouble() * 8)) + 128;
            float marginPercent = .25f;
            switch (direction)
            {
                case Direction.Up:
                    margin = (int)(chrome.Size.Height * marginPercent);
                    start.X = xy;
                    start.Y = chrome.Size.Height - margin;

                    goal.X = xy;
                    goal.Y = margin;
                    break;
                case Direction.Down:
                    margin = (int)(chrome.Size.Height * marginPercent);
                    start.X = xy;
                    start.Y = margin;

                    goal.X = xy;
                    goal.Y = chrome.Size.Height - margin;
                    break;
                case Direction.Left:
                    margin = (int)(chrome.Size.Width * marginPercent);
                    start.Y = xy;
                    start.X = chrome.Size.Width - margin;

                    goal.Y = xy;
                    goal.X = margin;
                    break;
                case Direction.Right:
                    margin = (int)(chrome.Size.Width * marginPercent);
                    start.Y = xy;
                    start.X = margin;

                    goal.Y = xy;
                    goal.X = chrome.Size.Width - margin;
                    break;
                default:
                    break;
            }

            FingerLifted = true;
            MousePosition = start;
            FingerLifted = false;
            MousePosition = goal;

        }
        private void LerpMouse()
        {
            while (true)
            {

                try
                {
                    if (_LerpMousePosition != null && _mousePosition != null)
                    {
                        if (_LerpMousePosition.Equals(_mousePosition) == false)
                        {
                            if (FingerLifted)
                            {
                                _LerpMouseTime = 1;
                            }
                            else
                            {
                                if (!_fingerTouching)
                                {
                                    Touch(CefSharp.Enums.TouchEventType.Pressed);
                                }
                            }
                            _LerpMousePosition = WiggleMouse(Lerp(_LerpStart, _mousePosition, _LerpMouseTime), 2);

                            SendMoveMouse(_LerpMousePosition);

                            if (_LerpMouseTime < 1)
                            {
                                _LerpMouseTime += .03f;

                            }
                            if (_LerpMouseTime >= 1)
                            {
                                _LerpMouseTime = 1;
                                _LerpMousePosition = _mousePosition;
                                FingerLifted = true;

                            }
                        }
                        else
                        {
                            if (_fingerTouching)
                            {
                                Touch(CefSharp.Enums.TouchEventType.Released);
                            }
                        }
                    }


                    Thread.Sleep(10);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }


            }
        }

        public Point CenterOfScreen()
        {
            return new Point(chrome.Size.Width / 2, chrome.Size.Height / 2);
        }

        public async Task<List<Cookie>> GetCookies()
        {
            if (CookieManager == null)
            {
                return null;
            }

            return await CookieManager.VisitAllCookiesAsync();

        }

        public static string URLWithoutWWW(string url)
        {
            return url.TrimStart();
        }

        public static bool CompareURL(string url1, string url2)
        {
            if (url1 == url2)
            {
                return true;
            }

            //Uri uri1 = new Uri(url1);
            Uri uri2;// = new Uri(url2);
            bool created2 = Uri.TryCreate(url2, UriKind.Absolute, out uri2);

            Uri uri1;
            bool created1 = Uri.TryCreate(url1, UriKind.Absolute, out uri1);
            if (created1)
            {

                //Uri uri1 = new Uri(url1);
                if (uri1.Host == uri2.Host)
                {
                    return true;
                }
            }

            string url2NoWWW = uri2.Host.Replace("www.", ".");
            if (url1 == uri2.Host || url1 == url2NoWWW)
            {
                return true;
            }
            return false;
        }

        public async Task<Cookie> GetCookie(string url, string name)
        {
            List<Cookie> cookies = await GetCookies();
            if (cookies != null)
            {

                foreach (Cookie cookie in cookies)
                {
                    if (name == cookie.Name)
                    {
                        if (CompareURL(cookie.Domain, url))
                        {
                            return cookie;
                        }
                    }
                }
            }

            return null;

        }
        public void SetSize(int width, int height)
        {
            SetSize(new Size(width, height));
        }
        public void SetSize(Size size)
        {
            chrome.Size = size;
        }

        private void UpdateUserAgent()
        {
            if (chrome != null)
            {

                if (chrome.RequestHandler != null)
                {
                    _requestHandler handler = (_requestHandler)chrome.RequestHandler;
                    handler._resourceRequestHandler._userAgent = this.UserAgent;
                }
            }
        }

        public Task Reload(bool ignoreCache = false)
        {
            return LoadPageAsync(null, true);
        }

        public PointF GetPointInRectangle(RectangleF rectangleF, float marginPercent = .25f)
        {
            float marginx = (rectangleF.Width / 2) * (marginPercent);
            float marginy = (rectangleF.Height / 2) * (marginPercent);
            float realW = rectangleF.Width - (marginx * 2);
            float realH = rectangleF.Height - (marginy * 2);
            float x = ((float)random.NextDouble() * realW) + rectangleF.X + marginx;
            float y = ((float)random.NextDouble() * realH) + rectangleF.Y + marginy;

            return new PointF(x, y);
        }

        private void SendMoveMouse(PointF point)
        {
            SendMoveMouse(point.X, point.Y);
        }

        private void Touch(CefSharp.Enums.TouchEventType type)
        {
            if (type == CefSharp.Enums.TouchEventType.Pressed)
            {
                _fingerTouching = true;
            }

            if (type == CefSharp.Enums.TouchEventType.Cancelled || type == CefSharp.Enums.TouchEventType.Released)
            {
                _fingerTouching = false;
            }
            CefSharp.Structs.TouchEvent touchEvent = new CefSharp.Structs.TouchEvent();
            touchEvent.PointerType = CefSharp.Enums.PointerType.Touch;
            touchEvent.Type = type;
            touchEvent.X = _LerpMousePosition.X;
            touchEvent.Y = _LerpMousePosition.Y;
            //Host.SendMouseMoveEvent((int)_LerpMousePosition.X, (int)_LerpMousePosition.Y, false, CefEventFlags.None);
            Host.SendTouchEvent(touchEvent);
            //Host.SendMouseClickEvent((int)_LerpMousePosition.X, (int)_LerpMousePosition.Y, MouseButtonType.Left, !_fingerTouching, 1, CefEventFlags.None);
        }
        private void SendMoveMouse(float x, float y)
        {

            Host.SendMouseMoveEvent((int)x, (int)y, false, CefEventFlags.None);
            if (_fingerTouching)
            {
                Touch(CefSharp.Enums.TouchEventType.Moved);
            }
        }

        public void ThrowExceptionIfNoCache()
        {
            if (Directory.Exists(cachePath.FullName) == false)
            {
                throw new Exception("Cache: " + cachePath.FullName + " doesn't exist!");
            }

            if (Directory.Exists(ScreenshotsPath.FullName) == false)
            {
                throw new Exception("Cache Screenshots: " + ScreenshotsPath.FullName + " doesn't exist!");
            }
        }

        public void CreateCacheDirectories()
        {
            if (Directory.Exists(cachePath.FullName) == false)
            {
                cachePath.Create();
            }

            if (Directory.Exists(ScreenshotsPath.FullName) == false)
            {
                ScreenshotsPath.Create();
            }
        }

        public bool ShouldForceUserAgentOnURL()
        {
            foreach (string url in ForceUserAgentOnURLs)
            {
                bool compare = CompareURL(chrome.Address, url);
                if (compare)
                {
                    return true;
                }
            }
            return false;
        }

        public Headless(string cachePath, string proxy, string userAgent = "", string url = "", Action<Headless> onInited = null, Action<Headless> onLoadError = null, params string[] forceUserAgentOnURLs)
        {
            this.ForceUserAgentOnURLs = forceUserAgentOnURLs;
            this.cachePath = new DirectoryInfo(cachePath);
            UserAgent = userAgent;
            ReadProxy(proxy);


            this.ScreenshotsPath = new DirectoryInfo(Path.Combine(cachePath, @"screenshots"));
            if (ScreenshotsPath.Exists == false)
            {
                ScreenshotsPath.Create();
            }

            browserSettings = new BrowserSettings();
            //Reduce rendering speed to one frame per second so it's easier to take screen shots
            browserSettings.WindowlessFrameRate = 30;
            requestContextSettings = new RequestContextSettings { CachePath = cachePath };

            requestContextSettings.PersistSessionCookies = false;
            requestContextSettings.PersistUserPreferences = false;

            requestContext = new RequestContext(requestContextSettings);
            //string data = @"data:,Hello%2C%20World!";
            //BrowserInitialized = false;
            chrome = new ChromiumWebBrowser((string.IsNullOrEmpty(url) ? IpCheckURL : url), browserSettings, requestContext);

            chrome.BrowserInitialized += (s, e) =>
            {
                try
                {

                    ChromiumWebBrowser br = (ChromiumWebBrowser)s;
                    CookieManager = chrome.GetCookieManager();
                    if (WebProxy != null)
                    {
                        var v = new Dictionary<string, object>
                        {
                            ["mode"] = "fixed_servers",
                            ["server"] = $"{WebProxy.Address.Scheme}://{WebProxy.Address.Host}:{WebProxy.Address.Port}"
                        };
                        if (!br.GetBrowser().GetHost().RequestContext.SetPreference("proxy", v, out string error))
                        {

                            Console.WriteLine(error);
                            throw new Exception(error);
                        }
                        Browser = br.GetBrowser();
                        Host = Browser.GetHost();

                        ThrowExceptionIfNoCache();

                        BrowserInitialized = true;
                        BrowserFailed = false;
                    }
                    else
                    {
                        throw new Exception("No proxy found!");
                    }

                }
                catch (Exception ex)
                {

                    BrowserFailed = true;
                    BrowserFailedMessage = ex.Message;
                    Console.WriteLine("Failed: " + ex.Message);
                }

                onInited?.Invoke(this);
            };

            

            chrome.LoadError += (s, e) =>
            {
                if (e.FailedUrl == chrome.Address)
                {

                    BrowserFailed = true;
                    BrowserFailedMessage = e.ErrorText;
                }
                else
                {
                    //a linked file failed to load
                    Console.WriteLine("Linked file failed to load!");
                }
                Console.WriteLine("Failed: " + e.ErrorText + " - " + e.FailedUrl);
                onLoadError?.Invoke(this);
            };

            if (WebProxy != null)
            {

                chrome.RequestHandler = new _requestHandler(WebProxy?.Credentials as System.Net.NetworkCredential, UserAgent);

            }
            PrepareFileForUpload();

            chrome.LifeSpanHandler = new _lifeSpanHandler();
            

            LerpMouseThread = new Thread(new ThreadStart(LerpMouse));
            LerpMouseThread.Name = "Browser Mouse Thread";
            LerpMouseThread.IsBackground = true;
            LerpMouseThread.Start();

            chrome.TitleChanged += (s, e) =>
            {
                this.Title = e.Title;
            };

            chrome.MenuHandler = new _menuHandler();

            EventHandler<LoadingStateChangedEventArgs> handler = null;
            handler = async (sender, args) =>
            {
                //Wait for while page to finish loading not just the first frame
                try
                {

                    if (!args.IsLoading)
                    {

                        await Task.Delay(PageLoadBufferTimeSeconds);

                        await UpdateCurrentLoadedUserAgentAsync();
                        if (ShouldForceUserAgentOnURL())
                        {

                            if (currentLoadedUserAgent != UserAgent)
                            {
                                Console.WriteLine("failed to overwrite the userAgent!");
                                //isLoading = false;
                                await Reload(true);
                            }
                            else
                            {

                                isLoading = false;

                                Console.WriteLine("Finished Loading: " + chrome.Address);
                            }
                        }
                        else
                        {
                            isLoading = false;
                        }

                        //MousePosition = CenterOfScreen();
                    }
                    //else
                    //{
                    //    isLoading = true;
                    //}
                }
                catch (Exception ex)
                {
                    isLoading = false;
                    Console.WriteLine(ex.Message);
                }
            };

            chrome.LoadingStateChanged += handler;


            LoadPageAsync();

            Cef.UIThreadTaskFactory.StartNew(delegate
            {
                if (requestContext.IsDisposed == false)
                {

                    var preferences = requestContext.GetAllPreferences(true);

                    //Check do not track status
                    bool doNotTrack = (bool)preferences["enable_do_not_track"];

                    Debug.WriteLine("DoNotTrack: " + doNotTrack);
                }
            });
        }

        public Task GoToURL(string url)
        {
            //await null;
            try
            {
                //await WaitToFinishLoading();
                Task task = LoadPageAsync(url);
                if (task != null)
                {

                    //task.Start();
                    //while (chrome.IsLoading)
                    //{
                    //    Thread.Sleep(1000);
                    //}
                    return task;
                }
                else
                {
                    return Task.Delay(10);// throw new Exception("Previous URL (" + chrome.Address + ") is stil loading...");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return Task.Delay(10);
                //throw new Exception("Failed to go to url: " + url + " with error:" + e.Message);
            }
        }

        public void Dispose()
        {try
            {
                if (Browser != null)
                {

                    Browser.CloseBrowser(true);
                    //if (!Browser.IsDisposed)
                    //{

                    //    Browser.Dispose();
                    //}
                }
                if (!requestContext.IsDisposed)
                {

                    requestContext.Dispose();
                }
                if (!chrome.IsDisposed)
                {

                    chrome.Dispose();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            
        }

        public void OverlayMouse(Image image)
        {
            int size = 8;
            int size2 = size * 2;
            using (Graphics g = Graphics.FromImage(image))
            {

                using (Brush brush = new SolidBrush(Color.Black))
                {
                    g.FillEllipse(brush, _mousePosition.X - size, _mousePosition.Y - size, size2, size2);
                    using (Pen pen = new Pen(brush))
                    {
                        g.DrawLine(pen, _mousePosition, _LerpMousePosition);
                    }
                }
                Color color = Color.Red;
                if (!FingerLifted || _fingerTouching)
                {
                    color = Color.Green;
                }
                using (Brush brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, _LerpMousePosition.X - size, _LerpMousePosition.Y - size, size2, size2);

                }

            }
        }
        public Bitmap TakeScreenshot(bool save = false, string path = "", bool overlayMouse = false)
        {
            try
            {

                if (chrome.IsBrowserInitialized)
                {
                    Bitmap bitmap = chrome.ScreenshotOrNull(PopupBlending.Blend);
                    if (overlayMouse)
                    {

                        OverlayMouse(bitmap);
                    }
                    if (save)
                    {

                        DisplayBitmap(bitmap, path);
                    }
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return null;
        }

        public bool PointIsOnScreen(PointF point, PointF bufferPercent)
        {
            return PointIsOnScreen(this, point, bufferPercent);
        }

        //
        //  0010
        //  0111
        //
        //
        public enum RectangleSide
        {
            TopLeft = 0,
            BottomLeft = 1,
            TopRight = 2,
            BottomRight = 3
        }

        public PointF[] GetRectanglePoints(RectangleF rectangleF)
        {
            PointF[] pointFs = new PointF[4];
            pointFs[(int)RectangleSide.TopLeft] = new PointF(rectangleF.X, rectangleF.Y);
            pointFs[(int)RectangleSide.BottomLeft] = new PointF(rectangleF.X, rectangleF.Y + rectangleF.Height);
            pointFs[(int)RectangleSide.TopRight] = new PointF(rectangleF.X + rectangleF.Width, rectangleF.Y);
            pointFs[(int)RectangleSide.BottomRight] = new PointF(rectangleF.X + rectangleF.Width, rectangleF.Y + rectangleF.Height);

            return pointFs;
        }

        public bool[] GetRectangleCornersInsideScreen(PointF[] points, PointF bufferPercent)
        {

            bool[] pointFs = new bool[4];
            pointFs[(int)RectangleSide.TopLeft] = PointIsOnScreen(points[(int)RectangleSide.TopLeft], bufferPercent);
            pointFs[(int)RectangleSide.BottomLeft] = PointIsOnScreen(points[(int)RectangleSide.BottomLeft], bufferPercent);
            pointFs[(int)RectangleSide.TopRight] = PointIsOnScreen(points[(int)RectangleSide.TopRight], bufferPercent);
            pointFs[(int)RectangleSide.BottomRight] = PointIsOnScreen(points[(int)RectangleSide.BottomRight], bufferPercent);

            return pointFs;
        }

        public PointF GetCenterOfRectangle(RectangleF rectangleF)
        {
            return new PointF(rectangleF.X + (rectangleF.Width / 2), rectangleF.Y + (rectangleF.Height / 2));
        }

        public static bool PointIsOnScreen(Headless webBrowser, PointF point, PointF bufferPercent)
        {
            float xbuffer = webBrowser.chrome.Size.Width * bufferPercent.X;
            float ybuffer = webBrowser.chrome.Size.Height * bufferPercent.Y;
            if (point.X > xbuffer && point.X < webBrowser.chrome.Size.Width - xbuffer && point.Y > ybuffer && point.Y < webBrowser.chrome.Size.Height - ybuffer)
            {
                return true;
            }

            return false;
        }

        public Task SendKey(System.Windows.Forms.Keys key)
        {

            CefSharp.KeyEvent keyEvent = new CefSharp.KeyEvent();
            keyEvent.WindowsKeyCode = (int)key;
            Host.SendKeyEvent(keyEvent);

            return Task.Delay(100);
        }

        public Task SendKey(char c)
        {

            CefSharp.KeyEvent keyEvent = new CefSharp.KeyEvent();
            keyEvent.WindowsKeyCode = c;
            keyEvent.Type = KeyEventType.Char;
            Host.SendKeyEvent(keyEvent);

            return Task.Delay(100);
        }

        public async Task<string> GetSource()
        {
            return await Browser.MainFrame.GetSourceAsync();
        }

        public async Task TypeInInputBox(string value, HTMLElement inputElement)
        {
            if (!inputElement.exists)
            {

                await inputElement.UpdateRectangleAsync(this);
            }
            if (inputElement.exists)
            {
                value = value.Replace("\r\n", "\n");
                await TouchElement(inputElement);
                string variable = "value";
                //if (inputElement.tag.ToLower() == "textarea")
                //{
                //    variable = "innerText";
                //}
                string searchValue = await inputElement.GetMyVariableData<string>(variable, this);
                int touchAttempts = 0;
                while (searchValue != value)
                {
                    int cursorPos = 0;
                    touchAttempts++;
                    //delete everything
                    while (searchValue.Length > 0)
                    {

                        await TouchElement(inputElement);
                        for (int i = 0; i < searchValue.Length; i++)
                        {
                            await SendKey(System.Windows.Forms.Keys.Back);
                            await Task.Delay(50);
                        }
                        searchValue = await inputElement.GetMyVariableData<string>(variable, this);
                    }

                    try
                    {
                        await Task.Delay(2000);
                        //type everything
                        string[] captionSections = value.Split(' ');
                        string writtenTotal = "";
                        for (int i = 0; i < captionSections.Length; i++)
                        {
                            string section = captionSections[i];
                            if (i < captionSections.Length-1)
                            {
                                section += " ";
                            }
                            //type each character in section

                            for (int j = 0; j < section.Length; j++)
                            {
                                int attempts = 0;
                                bool successful = false;

                                while(attempts < 10 && successful == false)
                                {
                                    char c = section[j];
                                    char typeC = c;
                                    if (typeC == '\n')
                                    {
                                        typeC = '\r';
                                    }

                                    await SendKey(typeC);

                                    await Task.Delay(500);

                                    searchValue = await inputElement.GetMyVariableData<string>(variable, this);
                                    if (ValueTypedSuccessfully(typeC, searchValue, cursorPos) == true)
                                    {
                                        cursorPos++;
                                        successful = true;
                                        
                                    }
                                    else
                                    {
                                        attempts++;
                                    }
                                }
                                
                            }
                            writtenTotal += section;

                            //verify the text for the section

                            searchValue = await inputElement.GetMyVariableData<string>(variable, this);

                            if ((searchValue != writtenTotal) || (string.IsNullOrEmpty(searchValue)))
                            {
                                throw new Exception("failed to type!");
                            }
                        }
                        //for (int i = 0; i < value.Length; i++)
                        //{

                        //    char c = value[i];
                        //    bool done = false;
                        //    int typeAttempts = 0;
                        //    while (!done)
                        //    {
                        //        typeAttempts++;
                        //        char typeC = c;
                        //        if (typeC == '\n')
                        //        {
                        //            typeC = '\r';
                        //            await SendKey(typeC);
                        //            //await SendKey(System.Windows.Forms.Keys.LineFeed);
                        //        }
                        //        else
                        //        {

                        //            await SendKey(typeC);


                        //        }
                        //        await Task.Delay(50);

                        //        searchValue = await inputElement.GetMyVariableData<string>(variable, this);

                        //        if (searchValue.Length > i)
                        //        {
                        //            char iChar = searchValue[i];
                        //            if (iChar == c)
                        //            {
                        //                done = true;
                        //                break;
                        //            }
                        //            else
                        //            {

                        //                await SendKey(System.Windows.Forms.Keys.Back);
                        //                await Task.Delay(50);
                        //            }
                        //        }

                        //        if (string.IsNullOrEmpty(searchValue))
                        //        {
                        //            if (typeAttempts >= 2)
                        //            {
                        //                throw new Exception("failed to type!");
                        //            }
                        //        }
                        //    }

                        //}
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }

                    if (touchAttempts > 2)
                    {
                        throw new Exception("Failed to touch input!");
                    }


                    searchValue = await inputElement.GetMyVariableData<string>(variable, this);


                }
            }
            else
            {
                throw new Exception("Input box doesn't exist to type into.");
            }
            
        }

        public bool ValueTypedSuccessfully(char value, string textboxValue, int pos)
        {
            if ((int)value > 31 && (int)value != 127)
            {
                //check if char at position value in textbox is equal to typeC
                if (!String.IsNullOrEmpty(textboxValue))
                {
                    if (textboxValue[pos] == value)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public Task InputType(string text)
        {
            return InputTypeAsync(text);
        }

        public async Task InputTypeAsync(string text)
        {
            Console.WriteLine("Typing: " + text);
            var browserHost = chrome.GetBrowserHost();
            var inputString = text;
            foreach (var c in inputString)
            {
                browserHost.SendKeyEvent(new KeyEvent { WindowsKeyCode = c, Type = KeyEventType.Char });
                await Task.Delay(100);
            }

            //Give the browser a little time to finish drawing our SendKeyEvent input
            await Task.Delay(1000);
        }

        public Task ClickElementById(string id)
        {
            return ClickElementByIdAsync(id);
        }

        public async Task ClickElementByIdAsync(string id)
        {

            Console.WriteLine("Clicking tag with id " + id);
            HTMLElement element = await GetElementByIdAsync(id);
            if (element.exists)
            {

                await TouchScreen(element);
            }
        }
        public Task FocusOnElementById(string id)
        {
            return ClickElementByIdAsync(id);
            //string script = "var _element = document.getElementById('" + id + "'); if (_element != null) {_element.focus();}";
            //return RunJavaScript(script);
        }

        public void PrepareFileForUpload(params string[] filePath)
        {
            chrome.DialogHandler = new _fileHandler(random, filePath);
        }

        public async Task ClickTagByAttributeAsync(string tag, string attributeName, string attributeValue)
        {
            Console.WriteLine("Clicking tag " + tag + " with atttribute " + attributeName + "='" + attributeValue + "'");
            HTMLElement element = await GetElementByAttributeAsync(tag, attributeName, attributeValue);

            if (element.exists)
            {

                await TouchScreen(element);
            }

        }
        public async Task ClickTagByInnerTextAsync(string tag, string text)
        {
            Console.WriteLine("Clicking tag " + tag + " with inner text '" + text + "'");
            HTMLElement element = await GetElementByInnerTextAsync(tag, text);

            if (element.exists)
            {

                await TouchScreen(element);
            }

        }

        public Direction GetSwipeDirectionToPoint(PointF point)
        {

            if (point.Y < 0)
            {
                return Direction.Down;
            }

            if (point.Y > chrome.Size.Height)
            {
                return Direction.Up;

            }
            if (point.X < 0)
            {
                return Direction.Right;
            }

            if (point.X > chrome.Size.Width)
            {
                return Direction.Left;
            }
            return Direction.None;
        }

        public async Task WaitForFingerLifted()
        {
            bool done = false;
            while (!done)
            {
                done = !_fingerTouching && FingerLifted;
                if (!done)
                {

                    await Task.Delay(1000);
                }
            }
        }

        public async Task<bool> ScrollToElement(HTMLElement element)
        {
            int timesPositionHasntChanged = 0;
            bool done = false;
            PointF lastCenter = new PointF();
            int total = 0;
            while (!done)
            {
                total++;
                if (total >= 30)
                {
                    done = true;
                    break;
                }
                if (element.exists)
                {

                    PointF center = GetCenterOfRectangle(element.rectangle);

                    if (center == lastCenter)
                    {
                        timesPositionHasntChanged++;
                    }
                    else
                    {
                        timesPositionHasntChanged = 0;
                    }

                    if (timesPositionHasntChanged > 10)
                    {
                        throw new Exception("scrolling but goal isnt moving closer... Aborting scroll..,");
                    }

                    PointF buffer = new PointF(0, .08f);
                    if (timesPositionHasntChanged > 0)
                    {
                        buffer = new PointF(0, 0);
                    }

                    done = PointIsOnScreen(center, buffer);
                    if (done)
                    {
                        return true;
                    }
                    if (!done)
                    {

                        Direction direction = GetSwipeDirectionToPoint(center);

                        Swipe(direction, center);
                        await WaitForFingerLifted();
                        await element.UpdateRectangleAsync(this);
                    }

                    lastCenter = center;
                }
                else
                {
                    done = true;
                }

            }

            return false;
        }

        public Task ClickLinkByHrefAsync(string href)
        {
            return ClickTagByAttributeAsync("a", "href", href);

        }
        public Task ClickLinkByTextAsync(string text)
        {
            return ClickTagByInnerTextAsync("a", text);
        }
        public Task ClickButtonByText(string text)
        {
            return ClickTagByInnerTextAsync("button", text);
        }

        public bool OnURL(string url)
        {
            return chrome.Address == url;
            //return CompareURL(chrome.Address, url);
        }

        public Task WaitToFinishLoading()
        {
            if (chrome != null)
            {
                if (chrome.IsBrowserInitialized)
                {

                    while (isLoading || chrome.IsLoading)
                    {
                        Thread.Sleep(1000);
                    }
                }
                else
                {

                    while (isLoading)
                    {
                        Thread.Sleep(1000);
                    }
                }
            }

            return Task.Delay(1000);
        }

        public Task TouchElement(HTMLElement element)
        {
            return TouchScreen(element);
        }
        private async Task TouchScreen(HTMLElement element)
        {
            try
            {
                if (element.exists == false)
                {
                    await element.UpdateRectangleAsync(this);
                }
                bool arrived = await ScrollToElement(element);
                if (arrived)
                {

                    await Task.Delay(1000);
                    FingerLifted = true;
                    MousePosition = GetPointInRectangle(element.rectangle);
                    FingerLifted = false;

                    if (SaveTouchScreenshots)
                    {
                        Bitmap bitmap = TakeScreenshot(true, Path.Combine(ScreenshotsPath.FullName, @"TouchEvent_" + DateTime.UtcNow.Ticks.ToString() + ".png"), true);
                        if (bitmap != null)
                        {

                        }

                    }

                    Touch(CefSharp.Enums.TouchEventType.Pressed);
                    await Task.Delay(500);
                    Touch(CefSharp.Enums.TouchEventType.Released);
                    FingerLifted = true;
                    await Task.Delay(500);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw e;
            }
        }

        public Task FocusInputByNameAsync(string name)
        {
            Console.WriteLine("Focusing on " + name);
            return ClickTagByAttributeAsync("input", "name", name);
        }


        public Task FocusInputByAttributeAsync(string attributeName, string attributeValue)
        {
            Console.WriteLine("Focusing on " + attributeName + "=" + attributeValue);
            return ClickTagByAttributeAsync("input", attributeName, attributeValue);
        }

        //public Task RunJavaScript(string javascript)
        //{

        //    return chrome.EvaluateScriptAsync(javascript);

        //}




        public async Task<HTMLElement> GetElementByAttributeAsync(string tag, string attributeName, string attributeValue)
        {
            HTMLElement element = new HTMLElement(tag, attributeName, attributeValue);
            await element.UpdateRectangleAsync(this);

            return element;
        }
        public async Task<HTMLElement> GetElementByInnerTextAsync(string tag, string innerText)
        {
            HTMLElement element = new HTMLElement(tag, innerText);
            await element.UpdateRectangleAsync(this);

            return element;
        }

        public async Task<HTMLElement> GetElementByIdAsync(string id)
        {
            HTMLElement element = new HTMLElement(id);
            await element.UpdateRectangleAsync(this);

            return element;

        }

        public async Task<object> GetJavaScriptResult(string javascript)
        {
            try
            {
                if (Browser != null)
                {

                    if (!Browser.IsDisposed)
                    {


                        if (!Browser.MainFrame.IsDisposed)
                        {

                            JavascriptResponse response = await Browser.MainFrame.EvaluateScriptAsync(javascript);//chrome.EvaluateScriptAsync(javascript);
                            if (!response.Success)
                            {
                                throw new Exception(response.Message);
                            }
                            object result = response.Success ? (response.Result ?? "null") : response.Message;
                            return result;
                        }

                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return null;

        }

        public static void InitializeChromium(string cachePath)
        {
            if (!Initialized)
            {
                var settings = new CefSettings()
                {
                    CachePath = cachePath,//Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CefSharp\\Cache"),
                    RootCachePath = cachePath,
                };
                
                CefSharpSettings.ShutdownOnExit = true;

                CefSharpSettings.SubprocessExitIfParentProcessClosed = true;
                Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
                Initialized = true;
            }

        }

        private void ReadProxy(string proxy)
        {
            if (string.IsNullOrEmpty(proxy) == false)
            {

                if (proxy.Contains("@"))
                {
                    //has auth
                    string[] proxyData = proxy.Split('@');

                    string[] proxyAuth = proxyData[0].Split(':');

                    ProxyUsername = proxyAuth[0];
                    ProxyPassword = proxyAuth[1];

                    proxy = proxyData[1];
                }


                string[] proxyHostData = proxy.Split(':');

                ProxyHost = proxyHostData[0];
                ProxyPort = proxyHostData[1];

                string proxyUrl = "http://" + ProxyHost + ":" + ProxyPort;
                WebProxy = new System.Net.WebProxy(proxyUrl, true, new[] { "" }, new System.Net.NetworkCredential(ProxyUsername, ProxyPassword));

            }
        }

        public Task UpdateCurrentLoadedUserAgent()
        {

            return UpdateCurrentLoadedUserAgentAsync();
        }


        public async Task UpdateCurrentLoadedUserAgentAsync()
        {
            try
            {
                currentLoadedUserAgent = (string)(await GetJavaScriptResult(@"navigator.userAgent"));

            }
            catch (Exception ex)
            {
                currentLoadedUserAgent = "";
                Console.WriteLine(ex);
            }
        }

        public Task LoadPageAsync(string address = null, bool reload = false)
        {
            if (!chrome.IsBrowserInitialized)
            {
                return Task.Delay(10);
            }
            if (!isLoading || reload)
            {
                //var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                //EventHandler<LoadingStateChangedEventArgs> handler = null;
                //handler = async (sender, args) =>
                //{
                //    //Wait for while page to finish loading not just the first frame
                //    if (!args.IsLoading)
                //    {
                //        chrome.LoadingStateChanged -= handler;
                //        await Task.Delay(PageLoadBufferTimeSeconds);
                //        //Important that the continuation runs async using TaskCreationOptions.RunContinuationsAsynchronously
                //        tcs.TrySetResult(true);
                //        isLoading = false;
                //        Console.WriteLine("Finished Loading: " + chrome.Address);

                //        await UpdateCurrentLoadedUserAgentAsync();

                //        if (currentLoadedUserAgent != UserAgent)
                //        {
                //            Console.WriteLine("failed to overwrite the userAgent!");
                //        }
                //        //MousePosition = CenterOfScreen();
                //    }
                //    else
                //    {
                //        isLoading = true;
                //    }
                //};

                //chrome.LoadingStateChanged += handler;
                if (!string.IsNullOrEmpty(address))
                {
                    isLoading = true;
                    chrome.Load(address);
                }
                else
                {
                    if (reload)
                    {

                        if (!string.IsNullOrEmpty(chrome.Address))
                        {

                            isLoading = true;
                            chrome.Reload(true);
                        }
                    }
                }
                return WaitToFinishLoading();
            }
            else
            {
                return Task.Delay(10);
            }
        }

        public static void DisplayBitmap(Bitmap bitmap, string path = "")
        {
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CefSharp screenshot" + DateTime.Now.Ticks + ".png");
            }
            // Make a file to save it to (e.g. C:\Users\jan\Desktop\CefSharp screenshot.png)
            var screenshotPath = path;

            Console.WriteLine();
            Console.WriteLine("Screenshot ready. Saving to {0}", screenshotPath);


            if (bitmap != null)
            {
                FileInfo fileInfo = new FileInfo(path);
                if (!fileInfo.Directory.Exists)
                {
                    fileInfo.Directory.Create();
                }
                // Save the Bitmap to the path.
                // The image type is auto-detected via the ".png" extension.
                bitmap.Save(screenshotPath);

                // We no longer need the Bitmap.
                // Dispose it to avoid keeping the memory alive.  Especially important in 32-bit applications.
                bitmap.Dispose();

                //Console.WriteLine("Screenshot saved.  Launching your default image viewer...");

                // Tell Windows to launch the saved image.
                //Process.Start(screenshotPath);

                //Console.WriteLine("Image viewer launched.  Press any key to exit.");

            }
            else
            {
                Console.WriteLine("Failed to take screenshot");
            }
        }

        private class _menuHandler : IContextMenuHandler
        {
            public void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
            {
                //throw new NotImplementedException();
                model.Clear();
            }

            public bool OnContextMenuCommand(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
            {
                return false;
                //throw new NotImplementedException();
            }

            public void OnContextMenuDismissed(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
            {
                //throw new NotImplementedException();
            }

            public bool RunContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
            {
                //throw new NotImplementedException();
                return false;
            }
        }
        private class _lifeSpanHandler : ILifeSpanHandler
        {
            public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
                return false;
                //throw new NotImplementedException();
            }

            public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {

            }

            public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {

            }

            public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
            {
                //chromiumWebBrowser.Load(targetUrl);
                newBrowser = null;
                return true;
            }
        }
        private class _fileHandler : IDialogHandler
        {
            public string[] filePath;
            public Random random;

            public _fileHandler(Random random, params string[] filePath)
            {
                this.filePath = filePath;
                this.random = random;
            }
            public bool OnFileDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFileDialogMode mode, CefFileDialogFlags flags, string title, string defaultFilePath, List<string> acceptFilters, int selectedAcceptFilter, IFileDialogCallback callback)
            {

                Thread.Sleep(1000 * random.Next(1, 5));
                callback.Continue(selectedAcceptFilter, filePath.ToList());
                return true;
                //throw new NotImplementedException();
            }
        }

        private class _resourceRequestHandler : ResourceRequestHandler
        {
            public string _userAgent;

            public _resourceRequestHandler(string userAgent = "")
            {
                _userAgent = userAgent;
            }
            public string GetInjection()
            {
                string setUserAgent = "";
                if (string.IsNullOrEmpty(_userAgent) == false)
                {
                    setUserAgent = @"navigator.__defineGetter__('userAgent', function () { return '" + _userAgent + "'; });";
                }
                return setUserAgent;
            }

            protected override IResponseFilter GetResourceResponseFilter(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response)
            {
                if (frame.IsValid)
                {

                    if (frame.IsMain && request.ResourceType == ResourceType.MainFrame)
                    {
                        return new JavascriptInjectionFilter(GetInjection());
                    }
                }


                return base.GetResourceResponseFilter(browserControl, browser, frame, request, response);
            }


            protected override CefReturnValue OnBeforeResourceLoad(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
            {
                Uri url;
                if (Uri.TryCreate(request.Url, UriKind.Absolute, out url) == false)
                {
                    //If we're unable to parse the Uri then cancel the request
                    // avoid throwing any exceptions here as we're being called by unmanaged code
                    return CefReturnValue.Cancel;
                }

                //Example of how to set Referer
                // Same should work when setting any header

                // For this example only set Referer when using our custom scheme
                //if (url.Scheme == CefSharpSchemeHandlerFactory.SchemeName)
                //{
                //    //Referrer is now set using it's own method (was previously set in headers before)
                //    request.SetReferrer("http://google.com", ReferrerPolicy.Default);
                //}

                //Example of setting User-Agent in every request.
                var headers = request.Headers;

                //var userAgent = headers["User-Agent"];
                if (string.IsNullOrEmpty(this._userAgent) == false)
                {

                    headers["User-Agent"] = this._userAgent;
                    headers["user-agent"] = this._userAgent;
                }

                request.Headers = headers;

                //NOTE: If you do not wish to implement this method returning false is the default behaviour
                // We also suggest you explicitly Dispose of the callback as it wraps an unmanaged resource.
                //callback.Dispose();
                //return false;

                //NOTE: When executing the callback in an async fashion need to check to see if it's disposed
                if (!callback.IsDisposed)
                {
                    using (callback)
                    {
                        if (request.Method == "POST")
                        {
                            using (var postData = request.PostData)
                            {
                                if (postData != null)
                                {
                                    var elements = postData.Elements;

                                    var charSet = request.GetCharSet();

                                    foreach (var element in elements)
                                    {
                                        if (element.Type == PostDataElementType.Bytes)
                                        {
                                            var body = element.GetBody(charSet);
                                        }
                                    }
                                }
                            }
                        }

                        //Note to Redirect simply set the request Url
                        //if (request.Url.StartsWith("https://www.google.com", StringComparison.OrdinalIgnoreCase))
                        //{
                        //    request.Url = "https://github.com/";
                        //}

                        //Callback in async fashion
                        //callback.Continue(true);
                        //return CefReturnValue.ContinueAsync;
                    }
                }

                return CefReturnValue.Continue;

                //return base.OnBeforeResourceLoad(browserControl, browser, frame, request, callback);
            }

        }
        private class _requestHandler : RequestHandler
        {
            private System.Net.NetworkCredential _credential;
            //public string _userAgent;
            //string injection = "window.InjectedObject = {};";
            public _resourceRequestHandler _resourceRequestHandler;


            public _requestHandler(System.Net.NetworkCredential credential = null, string userAgent = "") : base()
            {
                _credential = credential;
                //_userAgent = userAgent;
                _resourceRequestHandler = new _resourceRequestHandler(userAgent);

            }

            protected override IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
            {
                
                if (_resourceRequestHandler != null)
                {
                    return _resourceRequestHandler;
                }
                return base.GetResourceRequestHandler(chromiumWebBrowser, browser, frame, request, isNavigation, isDownload, requestInitiator, ref disableDefaultHandling);
            }



            protected override bool GetAuthCredentials(IWebBrowser browserControl, IBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
            {
                if (isProxy == true)
                {
                    if (_credential == null)
                        throw new NullReferenceException("credential is null");

                    callback.Continue(_credential.UserName, _credential.Password);
                    return true;
                }

                return false;
            }

        }

        public class JavascriptInjectionFilter : IResponseFilter
        {
            /// <summary>
            /// Location to insert the javascript
            /// </summary>
            public enum Locations
            {
                /// <summary>
                /// Insert Javascript at the top of the header element
                /// </summary>
                head,
                /// <summary>
                /// Insert Javascript at the top of the body element
                /// </summary>
                body
            }

            string injection;
            string location;
            int offset = 0;
            List<byte> overflow = new List<byte>();

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="injection"></param>
            /// <param name="location"></param>
            public JavascriptInjectionFilter(string injection, Locations location = Locations.head)
            {
                this.injection = "<script>" + injection + "</script>";
                switch (location)
                {
                    case Locations.head:
                        this.location = "<head>";
                        break;

                    case Locations.body:
                        this.location = "<body>";
                        break;

                    default:
                        this.location = "<head>";
                        break;
                }
            }

            /// <summary>
            /// Disposal
            /// </summary>
            public void Dispose()
            {
                //
            }

            /// <summary>
            /// Filter Processing...  handles the injection
            /// </summary>
            /// <param name="dataIn"></param>
            /// <param name="dataInRead"></param>
            /// <param name="dataOut"></param>
            /// <param name="dataOutWritten"></param>
            /// <returns></returns>
            public FilterStatus Filter(Stream dataIn, out long dataInRead, Stream dataOut, out long dataOutWritten)
            {
                try
                {
                    dataInRead = dataIn == null ? 0 : dataIn.Length;
                    dataOutWritten = 0;

                    if (overflow.Count > 0)
                    {
                        var buffersize = Math.Min(overflow.Count, (int)dataOut.Length);
                        dataOut.Write(overflow.ToArray(), 0, buffersize);
                        dataOutWritten += buffersize;

                        overflow.RemoveRange(0, buffersize);
                    }

                    //we need to inject the data before locating the header all at once to save on processing
                    List<byte> beforeInjection = new List<byte>();
                    byte[] afterInjection;
                    bool found = false;
                    for (var i = 0; i < dataInRead; ++i)
                    {
                        //long position = i + 1;
                        var readbyte = (byte)dataIn.ReadByte();
                        var readchar = Convert.ToChar(readbyte);
                        long buffersize = dataOut.Length - beforeInjection.Count;

                        if (buffersize > 0)
                        {
                            beforeInjection.Add(readbyte);
                            //dataOut.WriteByte(readbyte);
                            //dataOutWritten++;
                        }
                        else
                        {
                            overflow.Add(readbyte);
                        }

                        if (char.ToLower(readchar) == location[offset])
                        {
                            offset++;
                            if (offset >= location.Length)
                            {
                                //found location
                                found = true;
                                //write previous bytes. no need to check buffer length because it was already checked above. the beforeInjection makes it no matter what

                                dataOut.Write(beforeInjection.ToArray(), 0, beforeInjection.Count);
                                dataOutWritten += beforeInjection.Count;

                                //start injection
                                offset = 0;
                                buffersize = Math.Min(injection.Length, dataOut.Length - dataOutWritten);

                                if (buffersize >= injection.Length)
                                {
                                    var data = Encoding.UTF8.GetBytes(injection);
                                    dataOut.Write(data, 0, (int)buffersize);
                                    dataOutWritten += buffersize;
                                }
                                else
                                {
                                    var remaining = injection.Substring((int)buffersize, (int)(injection.Length - buffersize));
                                    overflow.AddRange(Encoding.UTF8.GetBytes(remaining));

                                }

                                //injection is over. add remaining bytes to the stream
                                int remainingBytes = (int)(dataInRead - (i + 1));
                                afterInjection = new byte[remainingBytes];
                                long afterBytesCount = dataIn.Read(afterInjection, 0, remainingBytes);
                                buffersize = Math.Min(afterBytesCount, dataOut.Length - dataOutWritten);

                                if (buffersize > 0)
                                {
                                    //can write all after data
                                    byte[] canWrite = afterInjection.Take((int)buffersize).ToArray();
                                    remainingBytes = afterInjection.Length - canWrite.Length;
                                    byte[] cantWrite = new byte[remainingBytes];
                                    //populate the cantWrite Array

                                    Array.Copy(afterInjection, buffersize, cantWrite, 0, remainingBytes); 
                                    //afterInjection.//1894

                                    dataOut.Write(canWrite, 0, (int)buffersize);
                                    dataOutWritten += buffersize;

                                    overflow.AddRange(cantWrite);
                                }
                                else
                                {
                                    overflow.AddRange(afterInjection);
                                    //if (buffersize > 0)
                                    //{
                                    //    //write portion and send rest to overflow
                                    //}
                                }

                                break;

                            }
                        }
                        else
                        {
                            offset = 0;
                        }

                    }
                    //end of for loop
                    if (!found)
                    {

                        //never found so never wrote
                        //write everything that fits
                        if (beforeInjection.Count > 0)
                        {
                            byte[] beforeInjectedArray = beforeInjection.ToArray();
                            long buffersize = Math.Min(beforeInjectedArray.Length, dataOut.Length - dataOutWritten);

                            if (buffersize > 0)
                            {

                                if (buffersize == beforeInjectedArray.Length)
                                {
                                    dataOut.Write(beforeInjectedArray, 0, (int)buffersize);
                                    dataOutWritten += buffersize;
                                }
                                else
                                {

                                    byte[] canWrite = beforeInjectedArray.Take((int)buffersize).ToArray();
                                    long remainingBytes = beforeInjectedArray.Length - canWrite.Length;
                                    byte[] cantWrite = new byte[remainingBytes];
                                    Array.Copy(beforeInjectedArray.ToArray(), buffersize, cantWrite, 0, remainingBytes);



                                    dataOut.Write(canWrite, 0, (int)buffersize);
                                    dataOutWritten += buffersize;

                                    overflow.AddRange(cantWrite);
                                }
                            }
                            else
                            {

                                overflow.AddRange(beforeInjectedArray);
                            }
                        }
                        else
                        {
                            //this was an extra page for previous data that didnt fit! :D
                        }
                        

                    }
                    if (overflow.Count > 0 || offset > 0)
                    {
                        return FilterStatus.NeedMoreData;
                    }

                    return FilterStatus.Done;
                }
                catch (Exception ex)
                {

                    throw ex;
                }
                
            }

            /// <summary>
            /// Initialization
            /// </summary>
            /// <returns></returns>
            public bool InitFilter()
            {
                return true;
            }

        }
    }
}