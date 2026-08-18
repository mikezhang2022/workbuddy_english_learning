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
        private GroupBox _grpPrompt;
        private TextBox _txtPrompt;
        private Button _btnSend;
        private GroupBox _grpAnswer;
        private RichTextBox _txtAnswer;
        private GroupBox _grpHistory;
        private ListView _lvSessions;
        private Label _lblRepo;
        private TextBox _txtRepo;

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
            _grpPrompt.SuspendLayout();
            _grpAnswer.SuspendLayout();
            _grpHistory.SuspendLayout();
            SuspendLayout();

            _root.ColumnCount = 2;
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _root.Dock = DockStyle.Fill;
            _root.Padding = new Padding(10);
            _root.RowCount = 7;
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 33F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 23F));

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

            _lblRepo.Text = "GitHub";
            _lblRepo.TextAlign = ContentAlignment.MiddleLeft;
            _lblRepo.Dock = DockStyle.Fill;

            _txtRepo.Dock = DockStyle.Fill;
            _txtRepo.Font = new Font("Segoe UI", 9F);
            _txtRepo.Text = "https://github.com/mikezhang2022/workbuddy_english_learning";
            _txtRepo.ForeColor = SystemColors.GrayText;
            _txtRepo.Enter += (s, e) =>
            {
                if (_txtRepo.Text == "https://github.com/mikezhang2022/workbuddy_english_learning")
                {
                    _txtRepo.Text = string.Empty;
                    _txtRepo.ForeColor = Color.Black;
                }
            };
            _txtRepo.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtRepo.Text))
                {
                    _txtRepo.Text = "https://github.com/mikezhang2022/workbuddy_english_learning";
                    _txtRepo.ForeColor = SystemColors.GrayText;
                }
            };

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
            _lvSessions.Columns.Add("Model", 140);
            _lvSessions.Columns.Add("Prompt", 520);
            _lvSessions.DoubleClick += LvSessions_DoubleClick;
            _grpHistory.Controls.Add(_lvSessions);

            _root.Controls.Add(_toolbar, 0, 0);
            _root.SetColumnSpan(_toolbar, 2);
            _root.Controls.Add(_lblModel, 0, 1);
            _root.Controls.Add(_cmbModel, 1, 1);
            _root.Controls.Add(_lblRepo, 0, 2);
            _root.Controls.Add(_txtRepo, 1, 2);
            _root.Controls.Add(_grpPrompt, 0, 3);
            _root.SetColumnSpan(_grpPrompt, 2);
            _root.Controls.Add(_btnSend, 0, 4);
            _root.SetColumnSpan(_btnSend, 2);
            _root.Controls.Add(_grpAnswer, 0, 5);
            _root.SetColumnSpan(_grpAnswer, 2);
            _root.Controls.Add(_grpHistory, 0, 6);
            _root.SetColumnSpan(_grpHistory, 2);

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(980, 720);
            Controls.Add(_root);
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(720, 520);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "CursorDesk";
            Load += MainForm_Load;

            _root.ResumeLayout(false);
            _toolbar.ResumeLayout(false);
            _grpPrompt.ResumeLayout(false);
            _grpAnswer.ResumeLayout(false);
            _grpHistory.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
