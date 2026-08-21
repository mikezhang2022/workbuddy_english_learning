using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CursorDesk.Api;
using CursorDesk.Core;
using CursorDesk.Storage;

namespace CursorDesk.App
{
    public partial class MainForm : Form
    {
        private CursorApiClient _api;
        private SqliteStore _store;
        private CancellationTokenSource _cts;
        private string _accountLabel = string.Empty;
        private bool _busy;
        private string _currentAgentId = string.Empty;
        private string _currentAgentUrl = string.Empty;
        private string _currentBranch = string.Empty;
        private string _licensedKey = string.Empty;
        private const string SettingAgentId = "currentAgentId";
        private const string SettingAgentUrl = "currentAgentUrl";
        private const string SettingRepoUrl = "repoUrl";
        private const string SettingBranch = "currentBranch";
        private const string DefaultRepoUrl = "https://github.com/mikezhang2022/workbuddy_english_learning";

        private const string ModeCloud = "cloud";
        private const string ModeLocal = "local";

        public MainForm(string apiKey)
        {
            InitializeComponent();
            _cts = new CancellationTokenSource();
            _licensedKey = apiKey ?? string.Empty;
        }

        private void DisposeRuntime()
        {
            try
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;
                }
            }
            catch
            {
            }

            if (_api != null)
            {
                _api.Dispose();
                _api = null;
            }
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            SetStatus("idle", null);
            LoadLocalHistory();

            string key;
            string error;
            if (!string.IsNullOrWhiteSpace(_licensedKey))
            {
                key = _licensedKey;
                _api = new CursorApiClient(key);
                await LoadModelsAsync().ConfigureAwait(true);
            }
            else
            {
                error = "授权 Key 无效，程序无法启动。";
                SetStatus("error", error);
                MessageBox.Show(error, "CursorDesk", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            LoadRepoUrl();
            if (_api != null)
            {
                _ = PreWarmAgentAsync();
            }
        }

        private void LoadRepoUrl()
        {
            EnsureStore();
            try
            {
                var saved = _store.GetSetting(SettingRepoUrl);
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    _txtRepo.Text = saved;
                    _txtRepo.ForeColor = Color.Black;
                }
            }
            catch (Exception)
            {
                // Keep the default placeholder.
            }
        }

        private void SaveRepoUrl(string repoUrl)
        {
            EnsureStore();
            try
            {
                _store.SetSetting(SettingRepoUrl, repoUrl ?? string.Empty);
            }
            catch (Exception)
            {
                // Non-fatal.
            }
        }



        private void RefreshControls()
        {
            _btnSend.Enabled = !_busy && _api != null;
            _btnValidate.Enabled = !_busy && _api != null;
            _cmbModel.Enabled = !_busy;
            _txtPrompt.ReadOnly = _busy;
        }

        private void LoadLocalHistory()
        {
            try
            {
                if (_store == null)
                {
                    _store = SqliteStore.CreateDefault();
                }

                BindHistory(_store.List());
            }
            catch (Exception ex)
            {
                SetStatus("error", "Could not open local session database: " + ex.Message);
            }
        }

        private async Task LoadModelsAsync()
        {
            EnsureDefaultModel();
            if (_api == null)
            {
                return;
            }

            try
            {
                SetStatus("working", "Loading models…");
                var models = await _api.GetModelsAsync(_cts.Token).ConfigureAwait(true);
                PopulateModels(models);
                SetStatus("idle", "Models loaded.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                EnsureDefaultModel();
                SetStatus("error", UserMessage(ex));
            }
        }

        private void EnsureDefaultModel()
        {
            if (_cmbModel.Items.Count == 0)
            {
                _cmbModel.Items.Add(CursorApiClient.DefaultModelId);
                _cmbModel.SelectedIndex = 0;
            }
        }

        private void PopulateModels(IList<ModelInfo> models)
        {
            var previous = _cmbModel.SelectedItem as string;
            _cmbModel.Items.Clear();

            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (models != null)
            {
                for (var i = 0; i < models.Count; i++)
                {
                    var id = models[i] == null ? null : models[i].Id;
                    if (string.IsNullOrWhiteSpace(id) || seen.Contains(id))
                    {
                        continue;
                    }

                    seen.Add(id);
                    _cmbModel.Items.Add(id);
                }
            }

            if (!seen.Contains(CursorApiClient.DefaultModelId))
            {
                _cmbModel.Items.Insert(0, CursorApiClient.DefaultModelId);
            }

            var select = previous;
            if (string.IsNullOrEmpty(select) || _cmbModel.Items.IndexOf(select) < 0)
            {
                select = CursorApiClient.DefaultModelId;
            }

            var index = _cmbModel.Items.IndexOf(select);
            _cmbModel.SelectedIndex = index >= 0 ? index : 0;
        }

        private async void BtnValidate_Click(object sender, EventArgs e)
        {
            if (_api == null)
            {
                SetStatus("error", "API key not loaded.");
                return;
            }

            try
            {
                SetBusy(true, "Validating key…");
                var me = await _api.GetMeAsync(_cts.Token).ConfigureAwait(true);
                _accountLabel = TextHelper.FormatAccount(me.UserEmail, me.DisplayName());
                SetStatus("idle", "Key valid.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (CursorApiException ex)
            {
                if (ex.StatusCode == 401)
                {
                    _accountLabel = string.Empty;
                    SetStatus("error", "invalid key");
                }
                else
                {
                    SetStatus("error", UserMessage(ex));
                }
            }
            catch (Exception ex)
            {
                SetStatus("error", UserMessage(ex));
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            if (_busy)
            {
                return;
            }

            if (_api == null)
            {
                SetStatus("error", "API key not loaded. Add key.txt next to the executable.");
                return;
            }

            var prompt = (_txtPrompt.Text ?? string.Empty).Trim();
            if (prompt.Length == 0)
            {
                SetStatus("error", "Enter a prompt first.");
                _txtPrompt.Focus();
                return;
            }

            var modelId = _cmbModel.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(modelId))
            {
                modelId = CursorApiClient.DefaultModelId;
            }

            try
            {
                SetBusy(true, "Sending…");
                _txtAnswer.Text = string.Empty;
                EnsureStore();

                // Resolve the GitHub repo (if any) so generated files land there under source/.
                var repoUrl = (_txtRepo.Text ?? string.Empty).Trim();
                if (!GitHubHelper.TryParseRepoUrl(repoUrl, out _, out _))
                {
                    repoUrl = string.Empty;
                }

                SaveRepoUrl(repoUrl);
                var repos = string.IsNullOrWhiteSpace(repoUrl)
                    ? null
                    : new List<string> { repoUrl };

                // Try to reuse a warm agent (skip VM cold start) if we have one stored.
                string agentId = LoadCurrentAgentId();
                if (!string.IsNullOrWhiteSpace(agentId) && !await IsAgentReusableAsync(agentId).ConfigureAwait(true))
                {
                    agentId = null;
                }

                string runId;
                if (string.IsNullOrWhiteSpace(agentId))
                {
                    // First call (or previous agent expired): pay the cold start once.
                    SetStatus("working", "Starting agent (first call is slower)…");
                    var created = await _api.CreateAgentAsync(prompt, modelId, _cts.Token, repos).ConfigureAwait(true);
                    if (created == null || string.IsNullOrWhiteSpace(created.Id))
                    {
                        throw new CursorApiException("Create agent returned no id.");
                    }

                    agentId = created.Id;
                    _currentAgentId = created.Id;
                    _currentAgentUrl = created.Url ?? string.Empty;
                    SaveCurrentAgent();
                    runId = created.ResolveRunId();
                }
                else
                {
                    // Warm reuse: no VM cold start.
                    SetStatus("working", "Reusing agent…");
                    var run = await _api.CreateRunAsync(agentId, prompt, modelId, _cts.Token, repos).ConfigureAwait(true);
                    runId = run == null ? null : run.Id;
                }

                if (string.IsNullOrWhiteSpace(runId))
                {
                    throw new CursorApiException("No run id was returned.");
                }

                // Stream tokens as they arrive (much faster perceived latency).
                SetStatus("working", "Streaming answer…");
                var sb = new StringBuilder();
                var answer = await _api.StreamRunAsync(
                    agentId,
                    runId,
                    token =>
                    {
                        sb.Append(token);
                        SetAnswerText(sb.ToString());
                    },
                    _cts.Token).ConfigureAwait(true);

                if (string.IsNullOrWhiteSpace(answer) && sb.Length > 0)
                {
                    answer = sb.ToString();
                }

                // If a repo was connected, silently save the branch for future use
                // but do NOT clutter the answer with URLs.
                if (!string.IsNullOrWhiteSpace(repoUrl))
                {
                    var branch = GitHubHelper.ParseBranchName(answer);
                    if (string.IsNullOrWhiteSpace(branch) && !string.IsNullOrWhiteSpace(_currentBranch))
                    {
                        branch = _currentBranch;
                    }

                    if (!string.IsNullOrWhiteSpace(branch))
                    {
                        _currentBranch = branch;
                        SaveBranch();
                    }
                }

                try { _txtAnswer.Rtf = FormatAnswer(answer); }
                catch { _txtAnswer.Text = answer; /* fallback if RTF is malformed */ }
                SaveSession(modelId, prompt, answer);
                SetStatus("done", "Finished.");
            }
            catch (OperationCanceledException)
            {
                SetStatus("idle", "Cancelled.");
            }
            catch (Exception ex)
            {
                SetStatus("error", UserMessage(ex));
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void SaveSession(string modelId, string prompt, string result, string mode = ModeCloud)
        {
            try
            {
                if (_store == null)
                {
                    _store = SqliteStore.CreateDefault();
                }

                var session = new Session
                {
                    Model = modelId,
                    Prompt = prompt,
                    Result = result ?? string.Empty,
                    Mode = mode ?? ModeCloud,
                    CreatedAt = DateTime.UtcNow
                };
                _store.Insert(session);
                BindHistory(_store.List());
            }
            catch (Exception ex)
            {
                SetStatus("error", "Saved answer, but history write failed: " + ex.Message);
            }
        }

        private void SetAnswerText(string text)
        {
            var rtf = FormatAnswer(text);
            if (_txtAnswer.InvokeRequired)
            {
                try
                {
                    _txtAnswer.Invoke((Action)(() =>
                    {
                        try { _txtAnswer.Rtf = rtf; }
                        catch { _txtAnswer.Text = text; /* fallback */ }
                    }));
                }
                catch (ObjectDisposedException)
                {
                }
            }
            else
            {
                try { _txtAnswer.Rtf = rtf; }
                catch { _txtAnswer.Text = text; /* fallback */ }
            }
        }

        /// <summary>
        /// Converts raw markdown-ish answer text into RichTextBox RTF.
        /// Handles: **bold**, *italic*, ##/### headings, --- rules, | tables,
        /// &gt; blockquotes, -/* lists, ```fenced code blocks``, and `inline code`.
        /// </summary>
        private static string FormatAnswer(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            // Strip any "URL:" or "GitHub files:" lines that may have been saved previously.
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var cleaned = new List<string>();
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("URL:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("GitHub files:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                cleaned.Add(line);
            }

            // Rejoin and collapse 3+ blank lines into 2.
            var raw = string.Join(Environment.NewLine, cleaned);
            raw = Regex.Replace(raw, @"(\r?\n\s*){3,}", Environment.NewLine + Environment.NewLine);

            return MarkdownToRtf(raw);
        }

        /// <summary>
        /// Converts a subset of Markdown to RTF so the RichTextBox can render
        /// bold, headings, code blocks, tables, lists, etc.
        /// </summary>
        private static string MarkdownToRtf(string md)
        {
            var rtf = new StringBuilder();
            rtf.Append(@"{\rtf1\ansi\ansicpg936\deff0\nouicompat\deflang1033\deflangfe2052");
            rtf.Append(@"{\fonttbl{\f0\fnil\fcharset134 \'b9\'a4\'c8\'a1\'ba\'c3\'b2\'b5;}{\f1\fmodern\fcharset0 Courier New;}}");
            rtf.Append(@"{\colortbl;\red0\green0\blue0;\red100\green100\blue100;\red240\green240\blue240;\red0\green100\blue0;}");
            rtf.Append(@"\viewkind4\uc1\pard\f0\fs22");

            var mdLines = md.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var i = 0;
            while (i < mdLines.Length)
            {
                var line = mdLines[i];
                var trimmed = line.Trim();

                // Blank line → paragraph break
                if (trimmed.Length == 0)
                {
                    rtf.Append(@"\par");
                    i++;
                    continue;
                }

                // Fenced code block ```lang ... ```
                if (trimmed.StartsWith("```"))
                {
                    i++; // skip opening ```
                    var codeLines = new List<string>();
                    while (i < mdLines.Length && !mdLines[i].Trim().StartsWith("```"))
                    {
                        codeLines.Add(mdLines[i]);
                        i++;
                    }
                    // skip closing ```
                    if (i < mdLines.Length) i++;

                    RenderCodeBlock(rtf, codeLines);
                    continue;
                }

                // Horizontal rule ---
                if (trimmed == "---" || trimmed == "***" || trimmed == "___")
                {
                    rtf.Append(@"\pard\brdrb\brdrs\brdrw10\brsp20 \par");
                    i++;
                    continue;
                }

                // Heading 1 #
                if (trimmed.StartsWith("# ") && !trimmed.StartsWith("## "))
                {
                    var headingText = trimmed.Substring(2).Trim();
                    rtf.Append(@"\pard\b\fs28 ");
                    rtf.Append(RtfEscape(headingText));
                    rtf.Append(@"\b0\fs22\par");
                    i++;
                    continue;
                }

                // Heading 2 ##
                if (trimmed.StartsWith("## ") && !trimmed.StartsWith("### "))
                {
                    var headingText = trimmed.Substring(3).Trim();
                    rtf.Append(@"\pard\b\fs24 ");
                    rtf.Append(RtfEscape(headingText));
                    rtf.Append(@"\b0\fs22\par");
                    i++;
                    continue;
                }

                // Heading 3 ###
                if (trimmed.StartsWith("### "))
                {
                    var headingText = trimmed.Substring(4).Trim();
                    rtf.Append(@"\pard\b\fs22 ");
                    rtf.Append(RtfEscape(headingText));
                    rtf.Append(@"\b0\par");
                    i++;
                    continue;
                }

                // Blockquote >
                if (trimmed.StartsWith("> "))
                {
                    var quoteText = trimmed.Substring(2).Trim();
                    rtf.Append(@"\pard\li200\cf2\i ");
                    rtf.Append(ProcessInline(quoteText));
                    rtf.Append(@"\i0\cf0\li0\par");
                    i++;
                    continue;
                }

                // Table row | ... |
                if (trimmed.Contains("|") && IsTableRow(trimmed))
                {
                    var tableRows = new List<string>();
                    while (i < mdLines.Length && IsTableRow(mdLines[i].Trim()))
                    {
                        var rowTrimmed = mdLines[i].Trim();
                        if (Regex.IsMatch(rowTrimmed, @"^\|[\s\-:|]+\|$"))
                        {
                            i++;
                            continue;
                        }
                        tableRows.Add(rowTrimmed);
                        i++;
                    }
                    if (tableRows.Count > 0)
                    {
                        RenderTableRtf(rtf, tableRows);
                    }
                    continue;
                }

                // Unordered list - / *
                if ((trimmed.StartsWith("- ") || trimmed.StartsWith("* ")) && !trimmed.StartsWith("***"))
                {
                    rtf.Append(@"\pard\li200\bullet ");
                    rtf.Append(ProcessInline(trimmed.Substring(2).Trim()));
                    rtf.Append(@"\li0\par");
                    i++;
                    continue;
                }

                // Ordered list 1. 2. etc.
                if (Regex.IsMatch(trimmed, @"^\d+\.\s+"))
                {
                    var match = Regex.Match(trimmed, @"^(\d+)\.\s+");
                    rtf.Append(@"\pard\li200 ");
                    rtf.Append(RtfEscape(match.Groups[1].Value + ". "));
                    rtf.Append(ProcessInline(trimmed.Substring(match.Length).Trim()));
                    rtf.Append(@"\li0\par");
                    i++;
                    continue;
                }

                // Regular paragraph with inline formatting
                rtf.Append(@"\pard ");
                rtf.Append(ProcessInline(trimmed));
                rtf.Append(@"\par");
                i++;
            }

            rtf.Append("}");
            return rtf.ToString();
        }

        /// <summary>
        /// Renders a fenced code block with monospace font and light gray background.
        /// </summary>
        private static void RenderCodeBlock(StringBuilder rtf, List<string> codeLines)
        {
            rtf.Append(@"\pard\cb3\fi200\f1\fs20 ");
            for (int c = 0; c < codeLines.Count; c++)
            {
                if (c > 0) rtf.Append(@"\par\pard\cb3\fi200\f1\fs20 ");
                rtf.Append(RtfEscape(codeLines[c]));
            }
            rtf.Append(@"\par\pard\cb0\f0\fs22 ");
        }

        /// <summary>
        /// Processes inline markdown: **bold**, *italic*, `code`.
        /// IMPORTANT: Returns raw RTF commands mixed with escaped text.
        /// The caller must NOT re-run RtfEscape on the result.
        /// </summary>
        private static string ProcessInline(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Escape everything first so RTF special chars in content are safe
            var safe = RtfEscape(text);

            // Now replace the escaped markers with RTF commands.
            // After RtfEscape: ** becomes \*\*, ` becomes \`

            // Code spans `...`  (escaped backtick is \')
            safe = Regex.Replace(safe, @"\\'([^\\']+)\\'", m =>
                "\\f1\\fs20 " + m.Groups[1].Value + "\\f0\\fs22");

            // Bold **...**  (escaped asterisk is \*)
            safe = Regex.Replace(safe, @"\\\*\\\*([^\\\*]+)\\\*\\\*", m =>
                "\\b " + m.Groups[1].Value + "\\b0 ");

            // Italic *...* (not bold, so not preceded/followed by \*)
            safe = Regex.Replace(safe, @"(?<!\\\*)\\\*([^\\\*]+)\\\*(?!\\\*)", m =>
                "\\i " + m.Groups[1].Value + "\\i0 ");

            return safe;
        }

        private static bool IsTableRow(string line)
        {
            var trimmed = line.Trim();
            return trimmed.StartsWith("|") && trimmed.EndsWith("|") && trimmed.Length > 2;
        }

        private static void RenderTableRtf(StringBuilder rtf, List<string> rows)
        {
            var allCells = new List<List<string>>();
            int maxCols = 0;
            foreach (var row in rows)
            {
                var cells = ParseTableCells(row);
                allCells.Add(cells);
                if (cells.Count > maxCols) maxCols = cells.Count;
            }

            for (int r = 0; r < allCells.Count; r++)
            {
                var rowCells = allCells[r];
                rtf.Append(@"\pard\li100 ");
                for (int c = 0; c < rowCells.Count; c++)
                {
                    if (c > 0) rtf.Append(@"\tab ");
                    if (r == 0) rtf.Append(@"\b");
                    rtf.Append(ProcessInline(rowCells[c].Trim()));
                    if (r == 0) rtf.Append(@"\b0");
                }
                rtf.Append(@"\li0\par");
            }
            rtf.Append(@"\pard\brdrb\brdrs\brdrw10\brsp20 \par");
        }

        private static List<string> ParseTableCells(string row)
        {
            var cells = new List<string>();
            var inner = row.Trim().Trim('|');
            var parts = inner.Split('|');
            foreach (var p in parts)
            {
                cells.Add(p.Trim());
            }
            return cells;
        }

        private static string RtfEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length * 2);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '{': sb.Append("\\{"); break;
                    case '}': sb.Append("\\}"); break;
                    case '\t': sb.Append("\\tab "); break;
                    case '\n': sb.Append("\\par "); break;
                    case '\r': break;
                    default:
                        if (c >= 0x00 && c <= 0x1f)
                        {
                            sb.AppendFormat("\\'{0:x2}", (int)c);
                        }
                        else if (c > 127)
                        {
                            // Unicode escape for CJK characters
                            sb.AppendFormat("\\u{0}?", (int)c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private void EnsureStore()
        {
            if (_store == null)
            {
                _store = SqliteStore.CreateDefault();
            }
        }

        private string LoadCurrentAgentId()
        {
            EnsureStore();
            var id = _store.GetSetting(SettingAgentId);
            if (!string.IsNullOrWhiteSpace(id))
            {
                _currentAgentId = id;
                _currentAgentUrl = _store.GetSetting(SettingAgentUrl) ?? string.Empty;
                _currentBranch = _store.GetSetting(SettingBranch) ?? string.Empty;
            }

            return id;
        }

        private void SaveCurrentAgent()
        {
            EnsureStore();
            _store.SetSetting(SettingAgentId, _currentAgentId);
            _store.SetSetting(SettingAgentUrl, _currentAgentUrl);
            _store.SetSetting(SettingBranch, _currentBranch);
        }

        private void SaveBranch()
        {
            EnsureStore();
            try
            {
                _store.SetSetting(SettingBranch, _currentBranch);
            }
            catch (Exception)
            {
                // Non-fatal.
            }
        }

        private async Task<bool> IsAgentReusableAsync(string agentId)
        {
            try
            {
                var agent = await _api.GetAgentAsync(agentId, _cts.Token).ConfigureAwait(true);
                return agent != null && agent.IsReusable();
            }
            catch (CursorApiException)
            {
                // 404 / expired / any error → treat as not reusable.
                return false;
            }
        }

        /// <summary>
        /// Pre-warms a agent in background so the first real question reuses it
        /// and skips the ~60s VM cold start. Fire-and-forget from MainForm_Load.
        /// </summary>
        private async Task PreWarmAgentAsync()
        {
            try
            {
                await Task.Delay(2000, _cts.Token).ConfigureAwait(true);

                var storedId = LoadCurrentAgentId();
                if (!string.IsNullOrWhiteSpace(storedId) && await IsAgentReusableAsync(storedId).ConfigureAwait(true))
                {
                    _currentAgentId = storedId;
                    _currentAgentUrl = _store.GetSetting(SettingAgentUrl) ?? string.Empty;
                    _currentBranch = _store.GetSetting(SettingBranch) ?? string.Empty;
                    SetStatus("idle", "Ready.");
                    return;
                }

                SetStatus("working", "Warming up agent (first call prepares it)…");

                // If the user configured a GitHub repo, warm with it too so the first real
                // (repo-bound) prompt reuses this agent instead of incurring a second cold start.
                var warmRepo = (_txtRepo.Text ?? string.Empty).Trim();
                if (!GitHubHelper.TryParseRepoUrl(warmRepo, out _, out _))
                {
                    warmRepo = string.Empty;
                }

                var warmRepos = string.IsNullOrWhiteSpace(warmRepo)
                    ? null
                    : new List<string> { warmRepo };

                var created = await _api.CreateAgentAsync(
                    "Reply with exactly: OK",
                    CursorApiClient.DefaultModelId,
                    _cts.Token,
                    warmRepos).ConfigureAwait(true);

                if (created != null && !string.IsNullOrWhiteSpace(created.Id))
                {
                    _currentAgentId = created.Id;
                    _currentAgentUrl = created.Url ?? string.Empty;
                    SaveCurrentAgent();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // Non-fatal: user will just experience cold start on first Send.
            }
            finally
            {
                if (!_busy)
                {
                    SetStatus("idle", "Ready.");
                }
            }
        }

        private void BindHistory(IList<Session> sessions)
        {
            _lvSessions.BeginUpdate();
            try
            {
                _lvSessions.Items.Clear();
                if (sessions == null)
                {
                    return;
                }

                for (var i = 0; i < sessions.Count; i++)
                {
                    var s = sessions[i];
                    var local = s.CreatedAt.Kind == DateTimeKind.Utc
                        ? s.CreatedAt.ToLocalTime()
                        : s.CreatedAt;
                    var item = new ListViewItem(local.ToString("yyyy-MM-dd HH:mm"));
                    var modeText = string.Equals(s.Mode, ModeLocal, StringComparison.Ordinal) ? "local" : "cloud";
                    var modelText = string.IsNullOrEmpty(s.Model) ? "—" : s.Model;
                    item.SubItems.Add(modeText);
                    item.SubItems.Add(modelText);
                    item.SubItems.Add(TextHelper.TruncateOneLine(s.Prompt, 80));
                    item.Tag = s;
                    _lvSessions.Items.Add(item);
                }
            }
            finally
            {
                _lvSessions.EndUpdate();
            }
        }

        private void LvSessions_DoubleClick(object sender, EventArgs e)
        {
            if (_lvSessions.SelectedItems.Count == 0)
            {
                return;
            }

            var session = _lvSessions.SelectedItems[0].Tag as Session;
            if (session == null)
            {
                return;
            }

            _txtPrompt.Text = session.Prompt ?? string.Empty;
            var resultText = session.Result ?? string.Empty;
            try { _txtAnswer.Rtf = FormatAnswer(resultText); }
            catch { _txtAnswer.Text = resultText; }
            if (!string.IsNullOrEmpty(session.Model))
            {
                var index = _cmbModel.Items.IndexOf(session.Model);
                if (index >= 0)
                {
                    _cmbModel.SelectedIndex = index;
                }
            }
        }

        private void SetBusy(bool busy, string workingMessage)
        {
            _busy = busy;
            RefreshControls();
            if (busy)
            {
                SetStatus("working", workingMessage);
            }
        }

        private void SetStatus(string state, string detail)
        {
            void Apply()
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(_accountLabel))
                {
                    parts.Add(_accountLabel);
                }

                parts.Add(state ?? "idle");
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    parts.Add(detail);
                }

                _lblStatus.Text = string.Join("  ·  ", parts.ToArray());
            }

            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    Invoke((Action)Apply);
                }
                catch (ObjectDisposedException)
                {
                }
            }
            else
            {
                Apply();
            }
        }

        private static string UserMessage(Exception ex)
        {
            var api = ex as CursorApiException;
            if (api != null)
            {
                return api.Message;
            }

            if (ex is System.Net.Http.HttpRequestException)
            {
                return "Network error: " + ex.Message;
            }

            return "Unexpected error: " + ex.Message;
        }

        private static string FirstNonEmpty(string a, string b)
        {
            if (!string.IsNullOrWhiteSpace(a))
            {
                return a;
            }

            if (!string.IsNullOrWhiteSpace(b))
            {
                return b;
            }

            return null;
        }
    }
}
