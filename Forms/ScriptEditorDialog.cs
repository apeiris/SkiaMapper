using ICSharpCode.TextEditor;
using SkiaMapper.Controls;
using SkiaMapper.Models;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SkiaMapper.Forms;

public class ScriptEditorDialog : Form {
    private TextEditorControl textEditor;
    private Button btnSave;
    private Button btnCancel;
    private FunctoidInstance? targetInstance;
    private SkiaMapperControl? parentControl;

    /// <summary>
    /// Gets or sets the script or text content of the editor.
    /// </summary>
    /// 
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ScriptContent {
        get => textEditor?.Text ?? string.Empty;
        set {
            if (textEditor != null) {
                textEditor.Text = value;
            }
        }
    }

    /// <summary>
    /// Constructor for plain text / XML schema editing.
    /// </summary>
    public ScriptEditorDialog(string initialContent, string title) {
        InitializeEditor(title, initialContent);
    }

    /// <summary>
    /// Constructor for FunctoidInstance script editing.
    /// </summary>
    public ScriptEditorDialog(FunctoidInstance instance, SkiaMapperControl control) {
        targetInstance = instance;
        parentControl = control;
        InitializeEditor($"Configure Script Block: {instance.CustomMethodName}", instance.CustomScriptBody ?? string.Empty);
    }

    private void InitializeEditor(string title, string content) {
        Text = title;
        Width = 700;
        Height = 500;
        StartPosition = FormStartPosition.CenterParent;

        // 1. Initialize ICSharpCode TextEditorControl
        textEditor = new TextEditorControl {
            Dock = DockStyle.Fill,
            Text = content
        };

        // Enable line numbers and show matching brackets
        textEditor.ShowLineNumbers = true;
        textEditor.IsReadOnly = false;

        // 2. Set Syntax Highlighting to C# (or XML based on content/title if desired)
        string highlightStrategy = title.Contains("XML", StringComparison.OrdinalIgnoreCase) ? "XML" : "C#";
        textEditor.Document.HighlightingStrategy =
            ICSharpCode.TextEditor.Document.HighlightingStrategyFactory.CreateHighlightingStrategy(highlightStrategy);

        // 3. Add Custom Context Menu Items
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
        MessageBox.Show(this, "Script formatting helper invoked.", "Format Script", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnSave_Click(object? sender, EventArgs e) {
        if (targetInstance != null && parentControl != null) {
            // Save back to functoid instance if called from canvas
            targetInstance.CustomScriptBody = textEditor.Text;
            parentControl.OnFunctoidScriptModified(targetInstance);
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}