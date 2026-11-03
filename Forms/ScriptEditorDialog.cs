using ICSharpCode.TextEditor;
using SkiaMapper.Controls;
using SkiaMapper.Models;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SkiaMapper.Forms;

public class ScriptEditorDialog : Form {
    private TextEditorControl textEditor;
    private Button btnSave;
    private Button btnCancel;
    private FunctoidInstance targetInstance;
    private SkiaMapperControl parentControl;

    public ScriptEditorDialog(FunctoidInstance instance, SkiaMapperControl control) {
        targetInstance = instance;
        targetInstance = instance;
        parentControl = control;

        Text = $"Configure Script Block: {instance.CustomMethodName}";
        Width = 700;
        Height = 500;
        StartPosition = FormStartPosition.CenterParent;

        // 1. Initialize ICSharpCode TextEditorControl
        textEditor = new TextEditorControl {
            Dock = DockStyle.Fill,
            Text = instance.CustomScriptBody ?? string.Empty
        };

        // Enable line numbers and show matching brackets
        textEditor.ShowLineNumbers = true;
        textEditor.IsReadOnly = false;

        // 2. Set Syntax Highlighting to C#
        textEditor.Document.HighlightingStrategy =
            ICSharpCode.TextEditor.Document.HighlightingStrategyFactory.CreateHighlightingStrategy("C#");

        // 3. Add Custom Context Menu Items (similar to your canvas setup)
        var editorContextMenu = textEditor.ContextMenuStrip ?? new ContextMenuStrip();

        var formatScriptItem = new ToolStripMenuItem("Format Script Block");
        formatScriptItem.Click += FormatScriptItem_Click;
        editorContextMenu.Items.Add(formatScriptItem);

        textEditor.ContextMenuStrip = editorContextMenu;

        // 4. Bottom Action Panel
        var panelBottom = new Panel {
            Dock = DockStyle.Bottom,
            Height = 45,
            BackColor = Color.FromArgb(240, 243, 248)
        };

        btnSave = new Button {
            Text = "Save & Apply",
            DialogResult = DialogResult.OK,
            Width = 100,
            Height = 30,
            Location = new Point(Width - 220, 8)
        };
        btnSave.Click += BtnSave_Click;

        btnCancel = new Button {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Width = 90,
            Height = 30,
            Location = new Point(Width - 110, 8)
        };

        panelBottom.Controls.Add(btnSave);
        panelBottom.Controls.Add(btnCancel);

        // Add controls to form
        Controls.Add(textEditor);
        Controls.Add(panelBottom);
    }

    private void FormatScriptItem_Click(object? sender, EventArgs e) {
        // Optional quick formatting action logic
        MessageBox.Show(this, "Script formatting helper invoked.", "Format Script", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnSave_Click(object? sender, EventArgs e) {
        // Save the updated script text back to the functoid instance
        targetInstance.CustomScriptBody = textEditor.Text;
        //targetInstance.IsScriptCustomized = true;

        // Notify the parent canvas control to re-analyze and redraw
        parentControl.OnFunctoidScriptModified(targetInstance);
        DialogResult = DialogResult.OK;
        Close();
    }
}