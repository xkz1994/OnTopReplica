using OnTopReplica.Native;
using OnTopReplica.Properties;
using System;
using System.Drawing;
using System.Windows.Forms;
using WindowsFormsAero.TaskDialog;

namespace OnTopReplica {
    //Contains some feature implementations of MainForm
    partial class MainForm {

        #region Click forwarding

        public bool ClickForwardingEnabled {
            get {
                return _thumbnailPanel.ReportThumbnailClicks;
            }
            set {
                if (value && Settings.Default.FirstTimeClickForwarding) {
                    TaskDialog dlg = new TaskDialog(Strings.InfoClickForwarding, Strings.InfoClickForwardingTitle, Strings.InfoClickForwardingContent) {
                        CommonButtons = CommonButton.Yes | CommonButton.No
                    };
                    if (dlg.Show(this).CommonButton == CommonButtonResult.No)
                        return;

                    Settings.Default.FirstTimeClickForwarding = false;
                }

                _thumbnailPanel.ReportThumbnailClicks = value;
            }
        }

        #endregion

        #region Click-through

        bool _clickThrough = false;

        readonly Color DefaultNonClickTransparencyKey;

        public bool ClickThroughEnabled {
            get {
                return _clickThrough;
            }
            set {
                TransparencyKey = (value) ? Color.Black : DefaultNonClickTransparencyKey;
                if (value) {
                    //Re-force as top most (always helps in some cases)
                    TopMost = false;
                    this.Activate();
                    TopMost = true;
                }

                _clickThrough = value;
            }
        }

        //Must NOT be equal to any other valid opacity value
        const double ClickThroughHoverOpacity = 0.6;

        Timer _clickThroughComeBackTimer = null;
        long _clickThroughComeBackTicks;
        const int ClickThroughComeBackTimerInterval = 1000;

        /// <summary>
        /// When the mouse hovers over a fully opaque click-through form,
        /// this fades the form to semi-transparency
        /// and starts a timeout to get back to full opacity.
        /// </summary>
        private void RefreshClickThroughComeBack() {
            if (this.Opacity == 1.0) {
                this.Opacity = ClickThroughHoverOpacity;
            }

            if (_clickThroughComeBackTimer == null) {
                _clickThroughComeBackTimer = new Timer();
                _clickThroughComeBackTimer.Tick += _clickThroughComeBackTimer_Tick;
                _clickThroughComeBackTimer.Interval = ClickThroughComeBackTimerInterval;
            }
            _clickThroughComeBackTicks = DateTime.UtcNow.Ticks;
            _clickThroughComeBackTimer.Start();
        }

        void _clickThroughComeBackTimer_Tick(object sender, EventArgs e) {
            var diff = DateTime.UtcNow.Subtract(new DateTime(_clickThroughComeBackTicks));
            if (diff.TotalSeconds > 2) {
                var mousePointer = WindowMethods.GetCursorPos();

                if (!this.ContainsMousePointer(mousePointer)) {
                    if (this.Opacity == ClickThroughHoverOpacity) {
                        this.Opacity = 1.0;
                    }
                    _clickThroughComeBackTimer.Stop();
                }
            }
        }

        #endregion

        #region Chrome

        readonly FormBorderStyle DefaultBorderStyle; // = FormBorderStyle.Sizable; // FormBorderStyle.SizableToolWindow;

        public bool IsChromeVisible {
            get {
                return (FormBorderStyle == DefaultBorderStyle);
            }
            set {
                //Cancel hiding chrome if no thumbnail is shown
                if (!value && !_thumbnailPanel.IsShowingThumbnail)
                    return;

                if (!value) {
                    Location = new Point {
                        X = Location.X + SystemInformation.FrameBorderSize.Width,
                        Y = Location.Y + SystemInformation.FrameBorderSize.Height
                    };
                    FormBorderStyle = FormBorderStyle.None;
                }
                else if(value) {
                    Location = new Point {
                        X = Location.X - SystemInformation.FrameBorderSize.Width,
                        Y = Location.Y - SystemInformation.FrameBorderSize.Height
                    };
                    FormBorderStyle = DefaultBorderStyle;
                }

                Program.Platform.OnFormStateChange(this);
                Invalidate();
            }
        }

        #endregion

        #region Position lock

        ScreenPosition? _positionLock = null;

        /// <summary>
        /// Gets or sets the screen position where the window is currently locked in.
        /// </summary>
        public ScreenPosition? PositionLock {
            get {
                return _positionLock;
            }
            set {
                if (value != null)
                    this.SetScreenPosition(value.Value);

                _positionLock = value;
            }
        }

        /// <summary>
        /// Refreshes window position if in lock mode.
        /// </summary>
        private void RefreshScreenLock() {
            //If locked in position, move accordingly
            if (PositionLock.HasValue) {
                this.SetScreenPosition(PositionLock.Value);
            }
        }

        #endregion

        #region Copy to Clipboard

        /// <summary>
        /// Copies the current thumbnail to clipboard.
        /// </summary>
        public void CopyThumbnailToClipboard() {
            if (_thumbnailPanel.IsShowingThumbnail == false || CurrentThumbnailWindowHandle == null)
                return;

            try {
                if (WindowMethods.GetWindowRect(CurrentThumbnailWindowHandle.Handle, out var rect) == false)
                    throw new System.ComponentModel.Win32Exception("GetWindowRect failed");

                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;

                if (width <= 0 || height <= 0)
                    throw new Exception("Invalid window size");

                using (var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
                    using (var graphics = Graphics.FromImage(bitmap)) {
                        var hdc = graphics.GetHdc();
                        try {
                            // Use PW_RENDERFULLCONTENT (2) flag to capture DirectComposition content
                            if (WindowMethods.PrintWindow(CurrentThumbnailWindowHandle.Handle, hdc, 2) == false)
                                throw new System.ComponentModel.Win32Exception("PrintWindow failed");
                        }
                        finally {
                            graphics.ReleaseHdc(hdc);
                        }
                    }

                    if (_thumbnailPanel.SelectedRegion != null && _thumbnailPanel.ConstrainToRegion) {
                        var region = _thumbnailPanel.SelectedRegion;
                        var sourceSize = _thumbnailPanel.ThumbnailOriginalSize;
                        var regionRect = region.ComputeRegionRectangle(sourceSize);

                        // Ensure region is within valid bounds
                        regionRect.Intersect(new Rectangle(0, 0, bitmap.Width, bitmap.Height));

                        if (regionRect.Width > 0 && regionRect.Height > 0) {
                            using (var croppedBitmap = bitmap.Clone(regionRect, bitmap.PixelFormat)) {
                                Clipboard.SetImage(croppedBitmap);
                            }
                        }
                    }
                    else {
                        Clipboard.SetImage(bitmap);
                    }
                }
            }
            catch (Exception ex) {
                Log.Write("Failed to copy to clipboard: {0}", ex.Message);
            }
        }

        #endregion

    }
}
