using System;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace CursorDesk.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnUiThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!LicenseGate.TryGetKey(out var realKey, out var licenseError, promptIfMissing: true))
            {
                MessageBox.Show(licenseError, "CursorDesk - 授权失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.Run(new MainForm(realKey));
        }

        private static void OnUiThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ShowError(e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            ShowError(ex);
        }

        private static void ShowError(Exception ex)
        {
            var message = ex == null ? "An unexpected error occurred." : ex.Message;
            try
            {
                MessageBox.Show(
                    message,
                    "CursorDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // Last-resort: never let the UI exception handler throw.
            }
        }
    }
}
