using System;
using System.Drawing;
using System.Windows.Forms;

namespace CursorDesk.App
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private TableLayoutPanel _root;
        private Panel _toolbar;
        private Button _btnValidate;
        private Label _lblStatus;
        private Label _lblModel;
        private ComboBox _cmbModel;
        private Label _lblMode;
        private ComboBox _cmbMode;
        private GroupBox _grpLocal;
        private TableLayoutPanel _localGrid;
        private Label _lblCliPath;
        private TextBox _txtCliPath;
        private Label _lblCapability;
        private ComboBox _cmbCapability;
        private Label _lblCliArgs;
        private TextBox _txtCliArgs;
        private Label _lblCliDir;
        private TextBox _txtCliDir;
        private GroupBox _grpPrompt;
        private TextBox _txtPrompt;
        private Button _btnSend;
        private GroupBox _grpAnswer;
        private RichTextBox _txtAnswer;
        private GroupBox _grpHistory;
        private ListView _lvSessions;
        private Label _lblRepo;
        private TextBox _txtRepo;

        // Row index of the "Local execution" settings row inside _root.
        private const int LocalRowIndex = 3;
        private const int LocalRowHeight = 150;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }

                DisposeRuntime();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            _root = new TableLayoutPanel();
            _toolbar = new Panel();
            _btnValidate = new Button();
            _lblStatus = new Label();
            _lblModel = new Label();
            _cmbModel = new ComboBox();
            _lblMode = new Label();
            _cmbMode = new ComboBox();
            _grpLocal = new GroupBox();
            _localGrid = new TableLayoutPanel();
            _lblCliPath = new Label();
            _txtCliPath = new TextBox();
            _lblCapability = new Label();
            _cmbCapability = new ComboBox();
            _lblCliArgs = new Label();
            _txtCliArgs = new TextBox();
            _lblCliDir = new Label();
            _txtCliDir = new TextBox();
            _grpPrompt = new GroupBox();
            _txtPrompt = new TextBox();
            _btnSend = new Button();
            _grpAnswer = new GroupBox();
            _txtAnswer = new RichTextBox();
            _grpHistory = new GroupBox();
            _lvSessions = new ListView();
            _lblRepo = new Label();
            _txtRepo = new TextBox();
            _root.SuspendLayout();
            _toolbar.SuspendLayout();
            _localGrid.SuspendLayout();
            _grpLocal.SuspendLayout();
            _grpPrompt.SuspendLayout();
            _grpAnswer.SuspendLayout();
            _grpHistory.SuspendLayout();
            SuspendLayout();

            _root.ColumnCount = 2;
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _root.Dock = DockStyle.Fill;
            _root.Padding = new Padding(10);
            _root.RowCount = 9;
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

            _toolbar.Dock = DockStyle.Fill;
            _btnValidate.Text = "Validate Key";
            _btnValidate.Width = 120;
            _btnValidate.Height = 28;
            _btnValidate.Location = new Point(0, 4);
            _btnValidate.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            _btnValidate.Click += BtnValidate_Click;

            _lblStatus.AutoEllipsis = true;
            _lblStatus.Location = new Point(130, 8);
            _lblStatus.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            _lblStatus.AutoSize = false;
            _lblStatus.Height = 22;
            _lblStatus.Text = "idle";
            _toolbar.Controls.Add(_btnValidate);
            _toolbar.Controls.Add(_lblStatus);
            _toolbar.Resize += delegate
            {
                _lblStatus.Width = Math.Max(80, _toolbar.ClientSize.Width - 140);
            };

            _lblModel.Text = "Model";
            _lblModel.TextAlign = ContentAlignment.MiddleLeft;
            _lblModel.Dock = DockStyle.Fill;

            _cmbModel.Dock = DockStyle.Fill;
            _cmbModel.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbModel.IntegralHeight = false;

            _lblMode.Text = "Mode";
            _lblMode.TextAlign = ContentAlignment.MiddleLeft;
            _lblMode.Dock = DockStyle.Fill;

            _cmbMode.Dock = DockStyle.Fill;
            _cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbMode.IntegralHeight = false;
            _cmbMode.Items.Add("Cloud (Cursor API)");
            _cmbMode.Items.Add("Local (CLI)");
            _cmbMode.SelectedIndex = 0;
            _cmbMode.SelectedIndexChanged += CmbMode_SelectedIndexChanged;

            _grpLocal.Text = "Local execution";
            _grpLocal.Dock = DockStyle.Fill;
            _grpLocal.Padding = new Padding(8, 4, 8, 4);

            _localGrid.ColumnCount = 2;
            _localGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
            _localGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _localGrid.Dock = DockStyle.Fill;
            _localGrid.RowCount = 4;
            _localGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _localGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _localGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _localGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

            _lblCliPath.Text = "CLI";
            _lblCliPath.TextAlign = ContentAlignment.MiddleLeft;
            _lblCliPath.Dock = DockStyle.Fill;

            _txtCliPath.Dock = DockStyle.Fill;
            _txtCliPath.Text = "cursor-agent";
            _txtCliPath.Font = new Font("Segoe UI", 9F);
            _txtCliPath.Leave += (s, e) => SaveLocalSettings();

            _lblCapability.Text = "Mode";
            _lblCapability.TextAlign = ContentAlignment.MiddleLeft;
            _lblCapability.Dock = DockStyle.Fill;

            _cmbCapability.Dock = DockStyle.Fill;
            _cmbCapability.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbCapability.IntegralHeight = false;
            _cmbCapability.Items.Add("Agent");
            _cmbCapability.Items.Add("Ask");
            _cmbCapability.Items.Add("Plan");
            _cmbCapability.SelectedIndex = 0;
            _cmbCapability.SelectedIndexChanged += CmbCapability_SelectedIndexChanged;

            _lblCliArgs.Text = "Args";
            _lblCliArgs.TextAlign = ContentAlignment.MiddleLeft;
            _lblCliArgs.Dock = DockStyle.Fill;

            _txtCliArgs.Dock = DockStyle.Fill;
            _txtCliArgs.Text = "-p --force --trust {prompt}";
            _txtCliArgs.Font = new Font("Segoe UI", 9F);
            _txtCliArgs.Leave += (s, e) => SaveLocalSettings();

            _lblCliDir.Text = "Dir";
            _lblCliDir.TextAlign = ContentAlignment.MiddleLeft;
            _lblCliDir.Dock = DockStyle.Fill;

            _txtCliDir.Dock = DockStyle.Fill;
            _txtCliDir.Text = "(app folder)";
            _txtCliDir.ForeColor = SystemColors.GrayText;
            _txtCliDir.Font = new Font("Segoe UI", 9F);
            _txtCliDir.Enter += (s, e) =>
            {
                if (_txtCliDir.Text == "(app folder)")
                {
                    _txtCliDir.Text = string.Empty;
                    _txtCliDir.ForeColor = Color.Black;
                }
            };
            _txtCliDir.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtCliDir.Text))
                {
                    _txtCliDir.Text = "(app folder)";
                    _txtCliDir.ForeColor = SystemColors.GrayText;
                }
                else
                {
                    SaveLocalSettings();
                }
            };

            _localGrid.Controls.Add(_lblCliPath, 0, 0);
            _localGrid.Controls.Add(_txtCliPath, 1, 0);
            _localGrid.Controls.Add(_lblCapability, 0, 1);
            _localGrid.Controls.Add(_cmbCapability, 1, 1);
            _localGrid.Controls.Add(_lblCliArgs, 0, 2);
            _localGrid.Controls.Add(_txtCliArgs, 1, 2);
            _localGrid.Controls.Add(_lblCliDir, 0, 3);
            _localGrid.Controls.Add(_txtCliDir, 1, 3);
            _grpLocal.Controls.Add(_localGrid);
            _grpLocal.Visible = false;

            _grpPrompt.Text = "Prompt";
            _grpPrompt.Dock = DockStyle.Fill;
            _txtPrompt.Multiline = true;
            _txtPrompt.ScrollBars = ScrollBars.Vertical;
            _txtPrompt.Dock = DockStyle.Fill;
            _txtPrompt.AcceptsReturn = true;
            _txtPrompt.Font = new Font("Segoe UI", 10F);
            _grpPrompt.Controls.Add(_txtPrompt);

            _btnSend.Text = "Send";
            _btnSend.Dock = DockStyle.Fill;
            _btnSend.Click += BtnSend_Click;

            _grpAnswer.Text = "Answer";
            _grpAnswer.Dock = DockStyle.Fill;
            _txtAnswer.Multiline = true;
            _txtAnswer.ReadOnly = true;
            _txtAnswer.ScrollBars = RichTextBoxScrollBars.Vertical;
            _txtAnswer.Dock = DockStyle.Fill;
            _txtAnswer.BackColor = Color.White;
            _txtAnswer.Font = new Font("Segoe UI", 10F);
            _txtAnswer.WordWrap = true;
            _txtAnswer.DetectUrls = false;
            _grpAnswer.Controls.Add(_txtAnswer);

            _grpHistory.Text = "Session history (double-click to reload)";
            _grpHistory.Dock = DockStyle.Fill;
            _lvSessions.Dock = DockStyle.Fill;
            _lvSessions.View = View.Details;
            _lvSessions.FullRowSelect = true;
            _lvSessions.HideSelection = false;
            _lvSessions.MultiSelect = false;
            _lvSessions.Columns.Add("Time", 150);
            _lvSessions.Columns.Add("Mode", 70);
            _lvSessions.Columns.Add("Model", 140);
            _lvSessions.Columns.Add("Prompt", 450);
            _lvSessions.DoubleClick += LvSessions_DoubleClick;
            _grpHistory.Controls.Add(_lvSessions);

            _root.Controls.Add(_toolbar, 0, 0);
            _root.SetColumnSpan(_toolbar, 2);
            _root.Controls.Add(_lblModel, 0, 1);
            _root.Controls.Add(_cmbModel, 1, 1);
            _root.Controls.Add(_lblMode, 0, 2);
            _root.Controls.Add(_cmbMode, 1, 2);
            _root.Controls.Add(_grpLocal, 0, LocalRowIndex);
            _root.SetColumnSpan(_grpLocal, 2);
            _root.Controls.Add(_lblRepo, 0, 4);
            _root.Controls.Add(_txtRepo, 1, 4);
            _root.Controls.Add(_grpPrompt, 0, 5);
            _root.SetColumnSpan(_grpPrompt, 2);
            _root.Controls.Add(_btnSend, 0, 6);
            _root.SetColumnSpan(_btnSend, 2);
            _root.Controls.Add(_grpAnswer, 0, 7);
            _root.SetColumnSpan(_grpAnswer, 2);
            _root.Controls.Add(_grpHistory, 0, 8);
            _root.SetColumnSpan(_grpHistory, 2);

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(980, 860);
            Controls.Add(_root);
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(720, 640);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "CursorDesk";
            Load += MainForm_Load;

            _root.ResumeLayout(false);
            _toolbar.ResumeLayout(false);
            _localGrid.ResumeLayout(false);
            _grpLocal.ResumeLayout(false);
            _grpPrompt.ResumeLayout(false);
            _grpAnswer.ResumeLayout(false);
            _grpHistory.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
