using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CursorDesk.Core
{
    /// <summary>
    /// Runs a local agent CLI (e.g. Cursor's `cursor-agent -p`) as a child process and streams
    /// its stdout/stderr back to the UI. This is the "local execution" alternative
    /// to the cloud Cursor Agent API.
    ///
    /// The args template may contain:
    ///   {prompt}     - the prompt as a quoted command-line argument (default).
    ///                  If the command line would exceed the Windows limit, the
    ///                  prompt is instead sent via stdin using the "-" convention.
    ///   {promptFile} - the prompt written to a temp file, passed as a quoted path.
    /// </summary>
    public sealed class LocalExecutor
    {
        // Windows command-line limit is 32767 chars; stay safely under it.
        private const int MaxCommandLineLength = 30000;

        public async Task<string> RunAsync(
            string prompt,
            string cliPath,
            string argsTemplate,
            string workingDirectory,
            Action<string> onOutput,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt is required.", nameof(prompt));
            }

            if (string.IsNullOrWhiteSpace(cliPath))
            {
                throw new ArgumentException("Local CLI path is required.", nameof(cliPath));
            }

            var template = argsTemplate ?? string.Empty;
            var promptFile = (string)null;
            try
            {
                bool useStdin;
                var arguments = BuildArguments(prompt, template, out promptFile, out useStdin);

                var psi = new ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = useStdin,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
                {
                    psi.WorkingDirectory = workingDirectory;
                }

                using (var process = new Process { StartInfo = psi })
                {
                    process.EnableRaisingEvents = true;

                    var output = new StringBuilder();
                    var outputLock = new object();

                    void AppendLine(string line)
                    {
                        var clean = TextHelper.StripAnsi(line);
                        lock (outputLock)
                        {
                            if (output.Length > 0)
                            {
                                output.AppendLine();
                            }
                            output.Append(clean);

                            if (!string.IsNullOrEmpty(clean) && onOutput != null)
                            {
                                onOutput(clean);
                            }
                        }
                    }

                    var tcs = new TaskCompletionSource<bool>();
                    process.Exited += (s, e) => tcs.TrySetResult(true);

                    try
                    {
                        process.Start();
                    }
                    catch (Win32Exception ex)
                    {
                        throw new InvalidOperationException(
                            "Could not start local CLI '" + cliPath + "'. " + ex.Message, ex);
                    }

                    process.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLine(e.Data); };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    using (cancellationToken.Register(() =>
                    {
                        try { if (!process.HasExited) process.Kill(); } catch { }
                        tcs.TrySetResult(true);
                    }))
                    {
                        if (useStdin)
                        {
                            // Write on a background thread: a child that never reads
                            // stdin would otherwise block us forever. Killing the
                            // process on cancel closes the pipe and unblocks the write.
                            var stdinTask = Task.Run(() =>
                            {
                                try
                                {
                                    var bytes = Encoding.UTF8.GetBytes(prompt);
                                    process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                                }
                                catch
                                {
                                    // Child closed stdin early; ignore.
                                }
                                finally
                                {
                                    try { process.StandardInput.Close(); } catch { }
                                }
                            });
                        }

                        await tcs.Task.ConfigureAwait(false);
                    }

                    // Let the async output handlers finish draining the pipes.
                    try { process.WaitForExit(); } catch { }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    return output.ToString();
                }
            }
            finally
            {
                if (promptFile != null)
                {
                    try { File.Delete(promptFile); } catch { }
                }
            }
        }

        private static string BuildArguments(string prompt, string template, out string promptFile, out bool useStdin)
        {
            promptFile = null;
            useStdin = false;

            var hasPrompt = template.IndexOf("{prompt}", StringComparison.Ordinal) >= 0;
            var hasPromptFile = template.IndexOf("{promptFile}", StringComparison.Ordinal) >= 0;

            if (hasPrompt)
            {
                var trial = template.Replace("{prompt}", TextHelper.EncodeArgument(prompt));
                if (trial.Length <= MaxCommandLineLength)
                {
                    return trial;
                }

                // Command line too long: read the prompt from stdin via "-".
                useStdin = true;
                return template.Replace("{prompt}", "-");
            }

            if (hasPromptFile)
            {
                var file = Path.GetTempFileName();
                File.WriteAllText(file, prompt, Encoding.UTF8);
                promptFile = file;
                return template.Replace("{promptFile}", TextHelper.EncodeArgument(file));
            }

            // No placeholder at all: append the prompt as a trailing argument.
            return template.TrimEnd() + " " + TextHelper.EncodeArgument(prompt);
        }
    }
}
