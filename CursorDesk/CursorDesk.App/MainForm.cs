using System;
using System.Collections.Generic;
using System.Text;
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
        private const string SettingAgentId = "currentAgentId";
        private const string SettingAgentUrl = "currentAgentUrl";

        public MainForm()
        {
            InitializeComponent();
            _cts = new CancellationTokenSource();
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
            if (!KeyStore.TryRead(out key, out error))
            {
                _btnSend.Enabled = false;
                _btnValidate.Enabled = false;
                SetStatus("error", error);
                MessageBox.Show(error, "CursorDesk", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _api = new CursorApiClient(key);
            await LoadModelsAsync().ConfigureAwait(true);
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
                    var created = await _api.CreateAgentAsync(prompt, modelId, _cts.Token).ConfigureAwait(true);
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
                    var run = await _api.CreateRunAsync(agentId, prompt, modelId, _cts.Token).ConfigureAwait(true);
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

                if (!string.IsNullOrWhiteSpace(_currentAgentUrl))
                {
                    answer = answer + Environment.NewLine + Environment.NewLine + "URL: " + _currentAgentUrl;
                }

                _txtAnswer.Text = answer;
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

        private void SaveSession(string modelId, string prompt, string result)
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
            if (_txtAnswer.InvokeRequired)
            {
                try
                {
                    _txtAnswer.Invoke((Action)(() => { _txtAnswer.Text = text; }));
                }
                catch (ObjectDisposedException)
                {
                }
            }
            else
            {
                _txtAnswer.Text = text;
            }
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
            }

            return id;
        }

        private void SaveCurrentAgent()
        {
            EnsureStore();
            _store.SetSetting(SettingAgentId, _currentAgentId);
            _store.SetSetting(SettingAgentUrl, _currentAgentUrl);
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
                    item.SubItems.Add(s.Model ?? string.Empty);
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
            _txtAnswer.Text = session.Result ?? string.Empty;
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
            _btnSend.Enabled = !busy && _api != null;
            _btnValidate.Enabled = !busy && _api != null;
            _cmbModel.Enabled = !busy;
            _txtPrompt.ReadOnly = busy;
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
