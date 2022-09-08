using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Runtime.InteropServices;
using System.Web;
using Microsoft.Win32;
using System.Diagnostics;

namespace FacebookWidget
{
    public partial class MainForm : Form, IMessageFilter
    {
        #region nested class

        public class UserInfo
        {
            public readonly string UserId;
            public readonly string Cookie;

            public UserInfo(string userId, string userName, Image avatar, string cookie)
            {
                UserId = userId;
                UserName = userName;
                Avatar = avatar;
                Cookie = cookie;
            }

            public string Status { get; set; } = null;

            public string UserName { get; set; } = null;

            public Image Avatar { get; set; } = null;

            public bool? IsOnline { get; set; } = null;
        }

        #endregion nested class

        #region variables

        private bool _bInitialized = false;
        private bool retrievalError = false;

        private HtmlAgilityPack.HtmlDocument _htmlDocument = null;

        private ContextMenuStrip _ctxMenu = null;

        #endregion variables

        #region const

        private const string ProcessName = "FacebookWidget";

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOMOVE = 0x0002;
        private const int TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;

        #endregion const

        #region constructors

        public MainForm()
        {
            InitializeComponent();

            Disposed += MainForm_Disposed;

            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)(768 | 3072);
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            Application.AddMessageFilter(this);
        }

        #endregion constructors

        #region properties

        private UserInfo userInfo { get; set; } = null;

        private int nMesseageCount { get; set; } = 0;

        #endregion properties

        #region events

        private void MainForm_Disposed(object sender, EventArgs e)
        {
            Application.RemoveMessageFilter(this);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (Process.GetProcessesByName(ProcessName).Length > 1)
            {
                tmrRefetchData.Enabled = false;
                tmrEnsureTopMost.Enabled = false;
                MessageBox.Show("Only 1 instance of FacebookWidget is allowed. End the currently running process first and try again.",
                    ProcessName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            _ctxMenu = new ContextMenuStrip();
            var _item = new ToolStripMenuItem("Close");
            _item.Click += (o, eArgs) => Application.Exit();

            _ctxMenu.Items.Add(_item);
            ContextMenuStrip = _ctxMenu;

            string _zCookie = null, _zUserId = null;

            if (File.Exists(Application.StartupPath + "\\Config.ini"))
            {
                var _configData = new IniConfigData(Application.StartupPath + "\\Config.ini");

                _zUserId = _configData.Read("ID", "Config");
                _zCookie = _configData.Read("Cookie", "Config");

                if (string.IsNullOrEmpty(_zUserId))
                {
                    MessageBox.Show("ID user not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                }

                //if (string.IsNullOrEmpty(_zCookie))
                //{
                //    MessageBox.Show("ID user not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //    Application.Exit();
                //}

                userInfo = new UserInfo(_zUserId, null, null, _zCookie);

                try
                {
                    //BackColor = Color.FromName(_configData.Read("TransparencyKey", "Config"));
                    //TransparencyKey = Color.FromName(_configData.Read("TransparencyKey", "Config"));
                }
                catch
                {
                    MessageBox.Show("Unsupported color name '" + _configData.Read("TransparencyKey", "Config") + "' in TransparencyKey", "FacebookWidget",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                }
            }
            else
            {
                MessageBox.Show("Cannot find the config file!", ProcessName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            RetrieveFacebookInfomation();

            NativeAPIs.SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);

            notifyIcon1.ShowBalloonTip(5000);

            tmrRefetchData.Start();
            tmrEnsureTopMost.Start();
            tmrUpdateSize.Start();

            tmrUpdateSize_Tick(tmrUpdateSize, EventArgs.Empty);
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            Helpers.GetTaskBarLocation(out Rectangle taskbarBounds);
            if (taskbarBounds.Width < 2 || taskbarBounds.Height < 2)
            {
                MessageBox.Show("Cannot find taskbar in screen", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
        }

        #endregion events

        protected override CreateParams CreateParams
        {
            get
            {
                var Params = base.CreateParams;
                Params.ExStyle |= 0x80;
                return Params;
            }
        }

        #region methods

        private void RetrieveFacebookInfomation()
        {
            if (userInfo == null)
                return;

            WebClient webClient = null;
            string _zResponse = null;

            try
            {
                webClient = new WebClient();
                webClient.Headers.Add("accept-language", "en;q=0.9,vi;q=0.8,fr-FR;q=0.7,fr;q=0.6,en-US;q=0.5");
                webClient.Headers.Add("cache-control", "max-age=0");
                webClient.Headers.Add("cookie", userInfo.Cookie);
                webClient.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"104\", \" Not A;Brand\";v=\"99\", \"Google Chrome\";v=\"104\"");
                webClient.Headers.Add("sec-ch-ua-mobile", "?0");
                webClient.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
                webClient.Headers.Add("sec-fetch-dest", "document");
                webClient.Headers.Add("sec-fetch-mode", "navigate");
                webClient.Headers.Add("sec-fetch-site", "none");
                webClient.Headers.Add("sec-fetch-user", "?1");
                webClient.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36");

                if (_htmlDocument == null)
                    _htmlDocument = new HtmlAgilityPack.HtmlDocument();

                if (!_bInitialized)
                {
                    using (var _data = webClient.OpenRead(string.IsNullOrEmpty(userInfo.Cookie) ?
                        ("https://mbasic.facebook.com/" + userInfo.UserId) :
                        ("https://mbasic.facebook.com/" + userInfo.UserId + "?v=timeline")))
                    using (var _sr = new StreamReader(_data))
                    {
                        _zResponse = _sr.ReadToEnd();

                        _htmlDocument.LoadHtml(_zResponse);

                        if (!string.IsNullOrEmpty(userInfo.Cookie))
                        {
                            var _zAvartarUrl = _htmlDocument.DocumentNode.
                                SelectSingleNode("//div[contains(@class, 'acw')]").
                                SelectSingleNode("//img[contains(@alt, 'profile picture')]").Attributes["src"].Value.Replace("&amp;", "&");

                            if (!string.IsNullOrEmpty(_zAvartarUrl))
                                picAvatar.Load(_zAvartarUrl);

                            var _zUserName = _htmlDocument.DocumentNode.SelectSingleNode("//span/div/span/strong").InnerText;
                            if (_zUserName.Contains("("))
                                _zUserName = _zUserName.Substring(0, _zUserName.IndexOf('(') - 1);

                            if (_zUserName.IndexOf('(') - 1 > 0)
                                userInfo.UserName = _zUserName.Substring(0, _zUserName.IndexOf('(') - 1);
                            else
                                userInfo.UserName = _zUserName;

                            try
                            {
                                if (_htmlDocument.DocumentNode.SelectSingleNode("//a[contains(@href, 'photos/change/profile_picture')]") != null)
                                    userInfo.UserName = _zUserName + " (You)";
                            }
                            catch
                            { }

                            try
                            {
                                userInfo.IsOnline = !HttpUtility.HtmlDecode(_htmlDocument.DocumentNode.
                                    SelectSingleNode("//span/div/span/img").Attributes["aria-label"].Value).Contains(_zUserName);
                            }
                            catch
                            {
                                userInfo.IsOnline = false;
                            }
                        }
                        else
                        {
                            var _zAvatarUrl = _htmlDocument.DocumentNode.
                                SelectSingleNode("//img[contains(@alt, 'profile')]").Attributes["src"].Value.Replace("&amp;", "&");

                            if (!string.IsNullOrEmpty(_zAvatarUrl))
                                picAvatar.Load(_zAvatarUrl);

                            userInfo.UserName = _htmlDocument.DocumentNode.SelectSingleNode("//div[@id='cover-name-root']").InnerText;
                            userInfo.IsOnline = null;
                        }

                        if (!string.IsNullOrEmpty(userInfo.Cookie))
                            _bInitialized = true;
                    }

                    var _zContent = string.Empty;
                    userInfo.Status = "0 unread messages";

                    //var _node = _htmlDocument.DocumentNode.SelectSingleNode("//a[contains(@href, 'messages')]");

                    _zContent = _htmlDocument.DocumentNode.SelectSingleNode("//a[contains(@href, 'messages')]")?.InnerText;

                    if (!string.IsNullOrEmpty(_zContent) && _zContent.Contains("(") && _zContent.Contains(")"))
                    {
                        _zContent = _htmlDocument.DocumentNode.SelectSingleNode("//nav/a[contains(@href, '" + userInfo.UserId + "') and contains(@aria-label, 'new message')]").InnerText;

                        if (_zContent.Contains("(") && _zContent.Contains(")"))
                        {
                            var p = _zContent.Substring(_zContent.IndexOf("(") + 1);
                            userInfo.Status = p.Substring(0, p.IndexOf(")")) + " unread message" + (p.Substring(0, p.IndexOf(")")) != "1" ? "s" : "");

                            if (p.Substring(0, p.IndexOf(")")) != "0" && p.Substring(0, p.IndexOf(")")) != nMesseageCount.ToString())
                            {
                                notifyIcon1.BalloonTipText = (lblUserName.Text.Contains(" (You)") ?
                                    "You have " :
                                    (lblUserName.Text + " send you ")) + p.Substring(0, p.IndexOf(")")) + " new message" + (p.Substring(0, p.IndexOf(")")) != "1" ? "s" : "") + ".";
                                notifyIcon1.ShowBalloonTip(15000);
                            }

                            nMesseageCount = int.Parse(p.Substring(0, p.IndexOf(")")));
                        }
                    }

                    _bInitialized = false;
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText(Application.StartupPath + "\\WidgetLog.log", "Response:\n" + _zResponse + "\nErrorInformation:\n" + ex.ToString());

                if (retrievalError == false)
                    MessageBox.Show("Cannot retrieve the user data!\nPlease check again the Facebook User ID" + (string.IsNullOrEmpty(userInfo.Cookie) ? ", or provide a Facebook cookie for a better retrieving" : ", or provide another cookie and try again.") + ".\nIf you have checked it but the error still occurs, you can contact the developer at GitHub.com/NozakiYuu.\n\nError information:\n" + ex.ToString() + "\n\nTo view more information, open the WidgetLog.log file in the same folder as the executable.", "FacebookWidget",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);

                retrievalError = true;
                userInfo.Status = "Cannot retrieve data!";
            }
            finally
            {
                if (webClient != null)
                {
                    webClient.Dispose();
                    webClient = null;
                }

                lblUserName.Text = userInfo.UserName;

                if (!string.IsNullOrEmpty(userInfo.Status))
                {
                    lblStatus.Text = userInfo.Status;
                }
                else
                {
                    var _bIsOnline = userInfo.IsOnline.GetValueOrDefault(false);
                    lblStatus.Text = _bIsOnline ? "Online" : "Offline";
                    lblStatus.ForeColor = _bIsOnline ? Color.Green : Color.Gray;
                }
            }
        }

        #endregion methods

        private void tmrRefetchData_Tick(object sender, EventArgs e)
        {
            RetrieveFacebookInfomation();
        }

        private void tmrEnsureTopMost_Tick(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Normal;
            BringToFront();
        }

        private void tmrUpdateSize_Tick(object sender, EventArgs e)
        {
            Helpers.GetTaskBarLocation(out Rectangle taskbarBounds);

            var _nPadding = Helpers.toCurrentDpi(6);
            var _nMinWeight = Math.Min(taskbarBounds.Width, taskbarBounds.Height);
            _nMinWeight -= _nPadding;

            var _avatarSize = new Size(_nMinWeight - picAvatar.Margin.Horizontal, _nMinWeight - picAvatar.Margin.Vertical);
            if (picAvatar.MaximumSize != _avatarSize)
                picAvatar.MaximumSize = picAvatar.MinimumSize = _avatarSize;

            var _textSize = TextRenderer.MeasureText(userInfo.UserName, Font);
            var _size = new Size(_textSize.Width + picAvatar.Width + picAvatar.Margin.Horizontal + lblUserName.Margin.Horizontal, _nMinWeight);
            if (Size != _size)
                Size = _size;

            var _location = taskbarBounds.Location + new Size(_nPadding / 2, _nPadding / 2);
            if (Location != _location)
                Location = _location;
        }

        private void moveByMouse()
        {
            if (MouseButtons == MouseButtons.Left)
            {
                NativeAPIs.ReleaseCapture();
                NativeAPIs.SendMessage(Handle, NativeAPIs.WM_NCLBUTTONDOWN, NativeAPIs.HT_CAPTION, 0);
            }
        }

        public bool PreFilterMessage(ref Message m)
        {
            if ((m.Msg == NativeAPIs.WM_LBUTTONDOWN || m.Msg == NativeAPIs.WM_NCLBUTTONDOWN) &&
                ClientRectangle.Contains(PointToClient(MousePosition)) && !_ctxMenu.Visible)
            {
                moveByMouse();
                return true;
            }

            return false;
        }
    }
}
