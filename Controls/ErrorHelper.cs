using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Text;

namespace SkiaMapper.Controls {
    public static class ErrorHelper {
        /// <summary>
        /// Displays an error message box and copies the complete exception message and stack trace to the clipboard.
        /// </summary>
        public static void HandleException(Exception ex, string title = "Execution Error") {
            var errorDetails = new StringBuilder();
            errorDetails.AppendLine($"=== ERROR MESSAGE ===");
            errorDetails.AppendLine(ex.Message);
            errorDetails.AppendLine();
            errorDetails.AppendLine($"=== EXCEPTION TYPE ===");
            errorDetails.AppendLine(ex.GetType().FullName);
            errorDetails.AppendLine();
            errorDetails.AppendLine($"=== STACK TRACE ===");
            errorDetails.AppendLine(ex.StackTrace);

            if (ex.InnerException != null) {
                errorDetails.AppendLine();
                errorDetails.AppendLine($"=== INNER EXCEPTION ===");
                errorDetails.AppendLine(ex.InnerException.Message);
                errorDetails.AppendLine(ex.InnerException.StackTrace);
            }

            string fullErrorText = errorDetails.ToString();

            // Copy complete diagnostic log to Clipboard safely
            try {
                Clipboard.SetText(fullErrorText);
            } catch {
                // Ignore clipboard locking edge-cases
            }

            // Show user-friendly alert notifying them that the error is copied
            MessageBox.Show(
                $"{ex.Message}\n\n(Full error details and stack trace have been copied to your clipboard.)",
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    public class AutoFadeMessageBox : Form {
        private System.Windows.Forms.Timer? _fadeTimer;

        public static void Show(string message, string title = "Notification", int durationMs = 2000) {
            // Ensure thread safety if called from background threads
            if (Application.OpenForms.Count > 0 && Application.OpenForms[0].InvokeRequired) {
                Application.OpenForms[0].Invoke(new Action(() => Show(message, title, durationMs)));
                return;
            }

            var box = new AutoFadeMessageBox(message, title, durationMs);
            box.Show();
        }

        private AutoFadeMessageBox(string message, string title, int durationMs) {
            Text = title;
            Size = new Size(320, 130);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            Opacity = 1.0;

            var label = new Label {
                Text = message,
                Left = 20,
                Top = 25,
                Width = 265,
                Height = 50,
                Font = new Font("Segoe UI", 10),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(label);

            // Timer to wait for the display duration, then trigger the fade-out
            var stayTimer = new System.Windows.Forms.Timer {
                Interval = Math.Max(500, durationMs - 500)
            };
            stayTimer.Tick += (s, e) => {
                stayTimer.Stop();
                stayTimer.Dispose();
                StartFadeOut();
            };
            stayTimer.Start();
        }

        private void StartFadeOut() {
            _fadeTimer = new System.Windows.Forms.Timer { Interval = 25 };
            _fadeTimer.Tick += (s, e) => {
                if (Opacity > 0.05) {
                    Opacity -= 0.05;
                } else {
                    _fadeTimer.Stop();
                    _fadeTimer.Dispose();
                    Close();
                }
            };
            _fadeTimer.Start();
        }
    }




    }

