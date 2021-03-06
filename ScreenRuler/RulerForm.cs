﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bluegrams.Application.WinForms;
using System.Configuration;
using ScreenRuler.Units;

namespace ScreenRuler
{
    public partial class RulerForm : Form
    {
        private MiniAppManager manager;
        private int mouseLine;

        public Settings Settings { get; set; }
        public bool Vertical { get { return Settings.Vertical; } }
        public LinkedList<int> CustomLines { get; set; }
        /// <summary>
        /// Gets/ sets the length of the ruler.
        /// </summary>
        public int RulerLength
        {
            get { return Vertical ? this.Height : this.Width; }
            set
            {
                if (Vertical) this.Height = value;
                else this.Width = value;
            }
        }

        public RulerForm()
        {
            Settings = new Settings();
            manager = new MiniAppManager(this, true) { AlwaysTrackResize = true };
            // Name all the properties we want to have persisted
            manager.AddManagedProperty(nameof(Settings));
            manager.AddManagedProperty(nameof(CustomLines), SettingsSerializeAs.Binary);
            manager.AddManagedProperties(nameof(Opacity), nameof(TopMost));
            manager.Initialize();
            InitializeComponent();
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true;
            this.TopMost = true;
            CustomLines = new LinkedList<int>();
            this.MouseWheel += RulerForm_MouseWheel;
        }

        private void RulerForm_Load(object sender, EventArgs e)
        {
            // Set some items of the context menu
            foreach (Enum item in Enum.GetValues(typeof(MeasuringUnit)))
            {
                comUnits.Items.Add(item.GetDescription());
            }
            // Reset the currently selected theme to avoid inconsistencies
            // caused by manual edits in the settings file.
            Settings.SelectedTheme = Settings.SelectedTheme;
            switch (this.Opacity*100)
            {
                case 100:
                    conHigh.Checked = true;
                    break;
                case 80:
                    conDefault.Checked = true;
                    break;
                case 60:
                    conLow.Checked = true;
                    break;
                case 40:
                    conVeryLow.Checked = true;
                    break;
            }
            // Check for updates
            Task.Run(() =>
            {
                manager.CheckForUpdates("https://screenruler.sourceforge.io/update.xml");
            });
        }

        #region Input Events
        //Message result codes of WndProc that trigger resizing:
        // HTLEFT = 10 -> in left resize area 
        // HTRIGHT = 11 -> in right resize area
        // HTTOP = 12 -> in upper resize area
        // HTBOTTOM = 15 -> in lower resize area
        private int FirstGrip { get { return Vertical ? 12 : 10; } }
        private int SecondGrip { get { return Vertical ? 15 : 11; } }

        // Use Windows messages to handle resizing of the ruler at the edges
        // and moving of the cursor marker.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) //WM_NCHITTEST (sent for all mouse events)
            {
                // Get mouse position and convert to app coordinates
                Point pos = Cursor.Position;
                pos = this.PointToClient(pos);
                int rpos = Vertical ? pos.Y : pos.X;
                // Move mouse marker
                mouseLine = rpos;
                this.Invalidate();
                // Check if inside grip area (5 pixels next to border)
                if (rpos <= 5)
                {
                    m.Result = (IntPtr)FirstGrip;
                    return;
                }
                else if (rpos >= (Vertical ? this.ClientSize.Height : this.ClientSize.Width) - 5)
                {
                    m.Result = (IntPtr)SecondGrip;
                    return;
                }
            }
            // Pass return message down to base class
            base.WndProc(ref m);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Shift) { resizeKeyDown(e); return; }
            else if (e.Alt) { dockKeyDown(e); return; }
            int step = e.Control ? 5 : 1;
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    conExit.PerformClick();
                    break;
                case Keys.S:
                    conTopmost.PerformClick();
                    break;
                case Keys.V:
                    conVertical.PerformClick();
                    break;
                case Keys.M:
                    conMarkCenter.PerformClick();
                    break;
                case Keys.T:
                    conMarkThirds.PerformClick();
                    break;
                case Keys.P:
                    conMarkMouse.PerformClick();
                    break;
                case Keys.Delete:
                    conClearCustomMarker.PerformClick();
                    break;
                case Keys.C:
                    if (CustomLines.Count > 0)
                    {
                        CustomLines.RemoveFirst();
                        this.Invalidate();
                    }
                    break;
                case Keys.L:
                    setMarkerToList(RulerLength);
                    this.Invalidate();
                    break;
                case Keys.F1:
                    conHelp.PerformClick();
                    break;
                default:
                    moveKeyDown(e);
                    break;
            }
            base.OnKeyDown(e);
        }

        /// <summary>
        /// Handles moving key events.
        /// </summary>
        private void moveKeyDown(KeyEventArgs e)
        {
            int step = e.Control ? 5 : 1;
            switch (e.KeyCode)
            {
                case Keys.Left:
                    this.Left -= step;
                    break;
                case Keys.Right:
                    this.Left += step;
                    break;
                case Keys.Up:
                    this.Top -= step;
                    break;
                case Keys.Down:
                    this.Top += step;
                    break;
            }
        }

        /// <summary>
        /// Handles resizing key events.
        /// </summary>
        private void resizeKeyDown(KeyEventArgs e)
        {
            int step = e.Control ? 5 : 1;
            switch (e.KeyCode)
            {
                case Keys.Left:
                    if (!Vertical) this.Width -= step;
                    break;
                case Keys.Right:
                    if (!Vertical) this.Width += step;
                    break;
                case Keys.Up:
                    if (Vertical) this.Height -= step;
                    break;
                case Keys.Down:
                    if (Vertical) this.Height += step;
                    break;
            }
        }

        /// <summary>
        /// Handles key events for docking to borders.
        private void dockKeyDown(KeyEventArgs e)
        {
            Screen screen = Screen.FromControl(this);
            switch(e.KeyCode)
            {
                case Keys.Left:
                    this.Left = screen.WorkingArea.Left;
                    break;
                case Keys.Right:
                    this.Left = screen.WorkingArea.Right - this.Width;
                    break;
                case Keys.Up:
                    this.Top = screen.WorkingArea.Top;
                    break;
                case Keys.Down:
                    this.Top = screen.WorkingArea.Bottom - this.Height;
                    break;
            }
        }


        private void RulerForm_MouseWheel(object sender, MouseEventArgs e)
        {
            // Resize according to mouse scroll direction.
            var amount = Math.Sign(e.Delta);
            RulerLength += amount;
        }

        private void RulerForm_MouseClick(object sender, MouseEventArgs e)
        {
            var position = Vertical ? e.Y : e.X;
            var line = CustomLines.Where((val) => Math.Abs(position - val) <= 2).FirstOrDefault();
            if (line != default(int))
            {
                CustomLineForm lineForm = new CustomLineForm(line,
                    new UnitConverter(Settings.MeasuringUnit, Settings.MonitorDpi), Settings.Theme);
                if (lineForm.ShowDialog(this) == DialogResult.OK)
                {
                    CustomLines.Remove(line);
                    this.Invalidate();
                }
            }
        }

        private void RulerForm_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // Add a marker at the cursor position.
            setMarkerToList(Vertical ? e.Y : e.X);
        }

        private void setMarkerToList(int pos)
        {
            // By default a single new marker is set, replacing the old one.
            if (!Settings.MultiMarking) CustomLines.Clear();
            CustomLines.AddLast(pos);
        }
        #endregion

        #region Draw Components
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Settings.Theme.Background);
            RulerPainter.Paint(e.Graphics, this.Size, Settings);
            RulerPainter.PaintMarkers(e.Graphics, this.Size, Settings, mouseLine, CustomLines);
            base.OnPaint(e);
        }
        #endregion

        #region Context Menu
        // Load current context menu state
        private void contxtMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            conVertical.Checked = Vertical;
            conMarkCenter.Checked = Settings.ShowCenterLine;
            conMarkThirds.Checked = Settings.ShowThirdLines;
            conTopmost.Checked = this.TopMost;
            conMarkMouse.Checked = Settings.ShowMouseLine;
            conMultiMarking.Checked = !Settings.MultiMarking;
            comUnits.SelectedIndex = (int)Settings.MeasuringUnit;
        }


        private void conMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void comUnits_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.MeasuringUnit = (MeasuringUnit)comUnits.SelectedIndex;
            this.Invalidate();
        }

        private void conMarkMouse_Click(object sender, EventArgs e)
        {
            Settings.ShowMouseLine = !Settings.ShowMouseLine;
            this.Invalidate();
        }

        private void conMarkCenter_Click(object sender, EventArgs e)
        {
            Settings.ShowCenterLine = !Settings.ShowCenterLine;
            this.Invalidate();
        }

        private void conMarkThirds_Click(object sender, EventArgs e)
        {
            Settings.ShowThirdLines = !Settings.ShowThirdLines;
            this.Invalidate();
        }

        private void conMultiMarking_Click(object sender, EventArgs e)
        {
            Settings.MultiMarking = !Settings.MultiMarking;
            if (CustomLines.Count > 0) setMarkerToList(CustomLines.Last.Value);
            this.Invalidate();
        }

        private void conClearCustomMarker_Click(object sender, EventArgs e)
        {
            CustomLines.Clear();
            this.Invalidate();
        }

        private void conTopmost_Click(object sender, EventArgs e) => TopMost = !TopMost;

        private void conVertical_Click(object sender, EventArgs e)
        {
            Settings.Vertical = !Settings.Vertical;
            changeVertical();
        }

        private void changeVertical()
        {
            this.Size = new Size(this.Height, this.Width);
            Rectangle windowRect = new Rectangle(this.Location, this.Size);
            Rectangle screenRect = Screen.FromRectangle(windowRect).WorkingArea;
            // If the ruler got out of the visible area, move it back in
            if (!screenRect.IntersectsWith(windowRect))
            {
                if (this.Left < screenRect.Left)
                    this.Left = screenRect.Left;
                else if (this.Left > screenRect.Right)
                    this.Left = screenRect.Right - this.Width;
                if (this.Top < screenRect.Top)
                    this.Top = screenRect.Top;
                else if (this.Top > screenRect.Bottom)
                    this.Top = screenRect.Bottom - this.Height;
            }
        }

        private void changeOpacity(object sender, EventArgs e)
        {
            foreach (ToolStripMenuItem it in conOpacity.DropDownItems)
                it.Checked = false;
            ((ToolStripMenuItem)sender).Checked = true;
            int opacity = int.Parse((String)((ToolStripMenuItem)sender).Tag);
            this.Opacity = (double)opacity / 100;
        }

        private void conLength_Click(object sender, EventArgs e)
        {
            SetSizeForm sizeForm = new SetSizeForm(RulerLength, Settings);
            if (sizeForm.ShowDialog(this) == DialogResult.OK)
            {
                this.RulerLength = sizeForm.RulerLength;
            }
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SettingsForm settingsForm = new SettingsForm(Settings);
            if (settingsForm.ShowDialog(this) == DialogResult.OK)
            {
                this.Invalidate();
            }
        }

        private void conHelp_Click(object sender, EventArgs e)
        {
            HelpForm helpForm = new HelpForm();
            helpForm.ShowDialog(this);
        }

        private void conAbout_Click(object sender, EventArgs e)
        {
            var resMan = new System.Resources.ResourceManager(this.GetType());
            var img = ((Icon)resMan.GetObject("$this.Icon")).ToBitmap();
            manager.ShowAboutBox(img);
        }

        private void conExit_Click(object sender, EventArgs e) => Close();
        #endregion

        // Handles dragging of the ruler
        #region Form Dragging
        private bool mouseDown;
        private Point mouseLoc;

        private void RulerForm_MouseDown(object sender, MouseEventArgs e)
        {
            mouseDown = true;
            mouseLoc = e.Location;
        }

        private void RulerForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseDown)
            {
                this.Location = new Point(this.Location.X - mouseLoc.X + e.X, this.Location.Y - mouseLoc.Y + e.Y);
            }
        }

        private void RulerForm_MouseUp(object sender, MouseEventArgs e)
        {
            mouseDown = false;
        }

        #endregion
    }
}
