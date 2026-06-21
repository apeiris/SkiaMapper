using SkiaMapper.Controls;
using SkiaMapper.Models;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SkiaMapper.Forms {
    public class ScriptEditorDialog : Form {
        private TextBox txtScript;
        private TextBox txtMethodName;
        private Button btnSave;
        private Button btnCancel;
        private FunctoidInstance _instance;
        private SkiaMapperControl _mapperControl; // Added field to hold the live control reference

        // REVISED CONSTRUCTOR: Accepts the active mapper control parent instance
        public ScriptEditorDialog(FunctoidInstance instance, SkiaMapperControl mapperControl) {
            _instance = instance;
            _mapperControl = mapperControl; // Store reference
            InitializeComponent();

            // Automatically binds the pre-built template script or custom modifications cleanly
            txtScript.Text = !string.IsNullOrWhiteSpace(instance.CustomScriptBody)
                ? instance.CustomScriptBody
                : "// No default script asset could be compiled.";

            txtMethodName.Text = !string.IsNullOrWhiteSpace(instance.CustomMethodName)
                ? instance.CustomMethodName
                : "TransformMethod";
        }

        private void InitializeComponent() {
            this.Text = $"Edit Script Context - {_instance.Definition?.Name}";
            this.Size = new Size(600, 450);
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(400, 300);

            // Method Name Label & Input
            Label lblMethod = new Label { Text = "Target Method Name:", Location = new Point(12, 15), Size = new Size(130, 20) };
            txtMethodName = new TextBox { Location = new Point(145, 12), Size = new Size(427, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            // Script Body Label & Textbox
            Label lblScript = new Label { Text = "C# Source Body Code:", Location = new Point(12, 45), Size = new Size(130, 20) };
            txtScript = new TextBox {
                Location = new Point(12, 68),
                Size = new Size(560, 280),
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 10F, FontStyle.Regular),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Buttons Actions Laydown
            btnSave = new Button { Text = "Save Changes", DialogResult = DialogResult.OK, Location = new Point(415, 365), Size = new Size(110, 30), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(300, 365), Size = new Size(100, 30), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };

            btnSave.Click += (s, e) => {
                _instance.CustomScriptBody = txtScript.Text;
                _instance.CustomMethodName = txtMethodName.Text;

                // FIXED: Execute the modification parser against the parent instance context
                if (_mapperControl != null) {
                    _mapperControl.OnFunctoidScriptModified(_instance);
                }

                this.Close();
            };

            this.Controls.AddRange(new Control[] { lblMethod, txtMethodName, lblScript, txtScript, btnSave, btnCancel });
        }
    }
}