using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace KeyGen
{
    public partial class MainForm : Form
    {
        private TextBox _txtMac;
        private TextBox _txtExp;
        private Button _btnGenerate;
        private Label _lblStatus;
        private TextBox _txtOutput;
        private Button _btnCopy;

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Key Generator (JWT)";
            Width = 560;
            Height = 440;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new System.Drawing.Font("Microsoft YaHei", 9F);

            var lblMac = new Label { Left = 16, Top = 18, Width = 480, Text = "MAC 地址：" };
            _txtMac = new TextBox { Left = 16, Top = 38, Width = 480, Text = "" };

            var lblExp = new Label { Left = 16, Top = 70, Width = 480, Text = "过期时间（yyyy-MM-dd 或 yyyy-MM-dd HH:mm:ss）：" };
            _txtExp = new TextBox { Left = 16, Top = 90, Width = 480, Text = "" };

            _btnGenerate = new Button { Left = 16, Top = 124, Width = 140, Height = 32, Text = "生成 Key" };
            _btnGenerate.Click += BtnGenerate_Click;

            _lblStatus = new Label { Left = 170, Top = 130, Width = 360, Height = 24, ForeColor = System.Drawing.Color.DarkRed, Text = "" };

            var lblOut = new Label { Left = 16, Top = 170, Width = 480, Text = "生成的 Key：" };
            _txtOutput = new TextBox
            {
                Left = 16,
                Top = 190,
                Width = 480,
                Height = 150,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = false
            };

            _btnCopy = new Button { Left = 16, Top = 350, Width = 140, Height = 32, Text = "复制到剪贴板" };
            _btnCopy.Click += BtnCopy_Click;

            Controls.AddRange(new Control[] { lblMac, _txtMac, lblExp, _txtExp, _btnGenerate, _lblStatus, lblOut, _txtOutput, _btnCopy });
        }

        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            _lblStatus.Text = "";
            _txtOutput.Text = "";

            // 1. Read base key from key.txt
            var secret = ReadBaseKey();
            if (secret == null)
            {
                _lblStatus.Text = "找不到 key.txt：" + ResolveKeyPath();
                return;
            }
            if (secret.Length == 0)
            {
                _lblStatus.Text = "key.txt 内容为空";
                return;
            }

            // 2. Validate MAC
            var macRaw = (_txtMac.Text ?? "").Trim();
            var mac = NormalizeMac(macRaw);
            if (mac == null)
            {
                _lblStatus.Text = "MAC 格式错误（应为 12 位十六进制，如 AA:BB:CC:DD:EE:FF）";
                return;
            }

            // 3. Parse expiration
            var expStr = (_txtExp.Text ?? "").Trim();
            long? exp = ParseExpiration(expStr);
            if (exp == null)
            {
                _lblStatus.Text = "过期时间格式错误（yyyy-MM-dd 或 yyyy-MM-dd HH:mm:ss）";
                return;
            }

            try
            {
                // The base key (key.txt) is both the HS256 signing secret and the
                // real key embedded in the payload ("叠加").
                var token = JwtHelper.CreateToken(secret, mac, exp.Value, secret);
                _txtOutput.Text = token;
                _lblStatus.ForeColor = System.Drawing.Color.DarkGreen;
                _lblStatus.Text = "生成成功（" + token.Length + " 字符）";
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = System.Drawing.Color.DarkRed;
                _lblStatus.Text = "生成失败：" + ex.Message;
            }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_txtOutput.Text)) return;
            Clipboard.SetText(_txtOutput.Text);
            _lblStatus.ForeColor = System.Drawing.Color.DarkGreen;
            _lblStatus.Text = "已复制到剪贴板";
        }

        // ---- helpers ----

        private static string ResolveKeyPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "key.txt");
        }

        private static string ReadBaseKey()
        {
            var path = ResolveKeyPath();
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path).Trim();
        }

        private static string NormalizeMac(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return null;
            var clean = Regex.Replace(mac, @"[^0-9A-Fa-f]", "");
            if (clean.Length != 12) return null;
            return clean.ToUpper();
        }

        private static long? ParseExpiration(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, out var dt))
            {
                return (long)(dt.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            }
            return null;
        }
    }

    /// <summary>
    /// Minimal HS256 JWT builder (no external dependencies).
    /// Token = base64url(header).base64url(payload).base64url(HMACSHA256(secret, data))
    /// </summary>
    internal static class JwtHelper
    {
        public static string CreateToken(string secret, string mac, long exp, string realKey)
        {
            var header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
            var iat = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            // mac (hex), exp/iat (integers) are safe; realKey is JSON-escaped.
            var payload = "{\"mac\":\"" + mac + "\",\"exp\":" + exp + ",\"iat\":" + iat +
                          ",\"key\":\"" + JsonEscape(realKey) + "\"}";

            var headerB64 = Base64Url(Encoding.UTF8.GetBytes(header));
            var payloadB64 = Base64Url(Encoding.UTF8.GetBytes(payload));
            var data = headerB64 + "." + payloadB64;

            byte[] sig;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            }

            return data + "." + Base64Url(sig);
        }

        private static string Base64Url(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string JsonEscape(string s)
        {
            if (s == null) return string.Empty;
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u" + ((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
