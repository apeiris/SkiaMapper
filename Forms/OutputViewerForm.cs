using System;
using System.IO;
using System.Windows.Forms;

namespace SkiaMapper.Forms {
    public class OutputViewerForm : Form {
        private WebBrowser webBrowser;

        public OutputViewerForm(string filePath) {
            Text = "Transformation Output Viewer";
            Width = 800;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;

            webBrowser = new WebBrowser {
                Dock = DockStyle.Fill
            };

            Controls.Add(webBrowser);

            if (File.Exists(filePath)) {
                webBrowser.Navigate(Path.GetFullPath(filePath));
            }
        }
    }
}