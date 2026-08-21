using System;
using System.Windows.Forms;

namespace CursorDesk.App
{
    /// <summary>
    /// Modal dialog that asks the user to paste the authorization key (JWT) when
    /// apikey.txt is missing or empty.
    /// </summary>
    internal class InputKeyForm : Form
    {
        public string Token { get; private set; }

        private TextBox _txt;
        private Button _btnOk;
        private Button _btnCancel;

        public InputKeyForm()
        {
            Text = "请输入授权 Key";
            Width = 560;
            Height = 260;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new System.Drawing.Font("Microsoft YaHei", 9F);

            var lbl = new Label
            {
                Left = 16,
                Top = 16,
                Width = 500,
                Height = 40,
                Text = "未找到 apikey.txt 或内容为空，请粘贴授权 Key（由 KeyGen 生成）："
            };

            _txt = new TextBox
            {
                Left = 16,
                Top = 60,
                Width = 510,
                Height = 90,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true
            };

            _btnOk = new Button { Left = 300, Top = 170, Width = 110, Height = 34, Text = "确定", DialogResult = DialogResult.OK };
            _btnCancel = new Button { Left = 420, Top = 170, Width = 110, Height = 34, Text = "取消", DialogResult = DialogResult.Cancel };

            _btnOk.Click += (s, e) => { Token = _txt.Text; };
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Controls.AddRange(new Control[] { lbl, _txt, _btnOk, _btnCancel });
        }
    }
}
