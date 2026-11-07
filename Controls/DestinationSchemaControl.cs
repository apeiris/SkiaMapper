using SkiaMapper.Forms;
using SkiaMapper.Models;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace SkiaMapper.Controls;

public class DestinationSchemaControl : SKControl {
    #region Events & Callbacks
    public event EventHandler<string>? DestinationPathChanged;
    public event EventHandler<string>? LogMessageRequested;
    public event EventHandler? DestinationSchemaReloaded;
    public event EventHandler<string>? NodeClicked;
    #endregion

    #region Fields & Properties
    private SchemaNode? _destinationRoot;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SchemaNode? DestinationRoot {
        get => _destinationRoot;
        set {
            _destinationRoot = value;
            _destinationNodeYCache.Clear();
            Invalidate();
        }
    }

    private string _destinationPath = string.Empty;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string DestinationPath {
        get => _destinationPath;
        set {
            _destinationPath = value;
            Invalidate();
        }
    }

    private float destinationVirtualScrollY = 0f;
    private float maxDestinationContentHeight = 0f;
    private bool isDraggingDestinationScrollbar = false;
    private float scrollbarDragStartY = 0f;
    private float scrollbarDragStartScrollY = 0f;

    private readonly Dictionary<string, float> _destinationNodeYCache = [];

    private SKRect destinationFileEllipsisRect;
    private SKRect destinationFileViewRect;
    private SKRect destinationFileEditRect;
    private const float HeaderHeight = 32f;

    private readonly SKFont _headerButtonFont = new(SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 12f);
    private readonly SKFont _headerTitleFont = new(SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 11f);
    private SKTypeface? iconTypeface;
    #endregion

    public DestinationSchemaControl() {
        this.DoubleBuffered = true;
        this.Dock = DockStyle.Fill;
        this.SetStyle(ControlStyles.Selectable, true);
        this.MouseWheel += DestinationSchemaControl_MouseWheel;

        try {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = "SkiaMapper.MaterialIcons-Regular.ttf";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null) {
                iconTypeface = SKTypeface.FromStream(stream);
            }
        } catch { }
    }

    public float? GetNodeWorkspaceY(string nodePath) {
        if (string.IsNullOrEmpty(nodePath)) return null;
        if (_destinationNodeYCache.TryGetValue(nodePath, out float contentY)) {
            return HeaderHeight + 8f + destinationVirtualScrollY + contentY;
        }
        return null;
    }

    public string? HitTestNodePath(float localY) {
        float contentMouseY = localY - (HeaderHeight + 8f + destinationVirtualScrollY);
        string foundPath = string.Empty;
        if (_destinationRoot != null && FindNodePathAtPosition(_destinationRoot, contentMouseY, "", ref foundPath)) {
            return foundPath;
        }
        return null;
    }

    private void DestinationSchemaControl_MouseWheel(object? sender, MouseEventArgs e) {
        float viewableHeight = Height - HeaderHeight;
        if (maxDestinationContentHeight <= viewableHeight) {
            destinationVirtualScrollY = 0f;
            Invalidate();
            return;
        }

        int scrollDelta = e.Delta > 0 ? 30 : -30;
        destinationVirtualScrollY += scrollDelta;
        float minScrollY = -(maxDestinationContentHeight - viewableHeight);
        destinationVirtualScrollY = Math.Clamp(destinationVirtualScrollY, minScrollY, 0f);

        Invalidate();
        this.Parent?.Invalidate();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
        base.OnPaintSurface(e);
        _destinationNodeYCache.Clear();

        var canvas = e.Surface.Canvas;
        int w = e.Info.Width;
        int h = e.Info.Height;
        canvas.Clear(SKColors.GhostWhite);

        SKRect rightHeaderRect = new SKRect(0, 0, w, HeaderHeight);
        using (var headerBg = new SKPaint { Color = new SKColor(230, 235, 242), Style = SKPaintStyle.Fill, IsAntialias = true }) {
            canvas.DrawRect(rightHeaderRect, headerBg);
        }
        using (var headerBorder = new SKPaint { Color = new SKColor(180, 190, 205), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true }) {
            canvas.DrawLine(0, HeaderHeight, w, HeaderHeight, headerBorder);
        }

        float btnWidth = 24f;
        float btnHeight = 20f;
        destinationFileEllipsisRect = new SKRect(w - btnWidth - 6f, (HeaderHeight - btnHeight) / 2f, w - 6f, (HeaderHeight + btnHeight) / 2f);
        destinationFileViewRect = new SKRect(destinationFileEllipsisRect.Left - btnWidth - 4f, destinationFileEllipsisRect.Top, destinationFileEllipsisRect.Left - 4f, destinationFileEllipsisRect.Bottom);
        destinationFileEditRect = new SKRect(destinationFileViewRect.Left - btnWidth - 4f, destinationFileViewRect.Top, destinationFileViewRect.Left - 4f, destinationFileViewRect.Bottom);

        using (var btnBg = new SKPaint { Color = new SKColor(210, 220, 235), Style = SKPaintStyle.Fill, IsAntialias = true })
        using (var btnBorder = new SKPaint { Color = new SKColor(160, 175, 195), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true })
        using (var btnTextPaint = new SKPaint { Color = new SKColor(40, 50, 70), IsAntialias = true })
        using (var iconStroke = new SKPaint { Color = new SKColor(40, 50, 70), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true })
        using (var iconFill = new SKPaint { Color = new SKColor(40, 50, 70), Style = SKPaintStyle.Fill, IsAntialias = true }) {

            canvas.DrawRoundRect(destinationFileEllipsisRect, 3f, 3f, btnBg);
            canvas.DrawRoundRect(destinationFileEllipsisRect, 3f, 3f, btnBorder);
            canvas.DrawText("...", destinationFileEllipsisRect.MidX, destinationFileEllipsisRect.MidY + 4f, SKTextAlign.Center, _headerButtonFont, btnTextPaint);

            canvas.DrawRoundRect(destinationFileViewRect, 3f, 3f, btnBg);
            canvas.DrawRoundRect(destinationFileViewRect, 3f, 3f, btnBorder);
            float cx = destinationFileViewRect.MidX, cy = destinationFileViewRect.MidY;
            using (var eyePath = new SKPath()) {
                eyePath.MoveTo(cx - 7f, cy);
                eyePath.QuadTo(cx, cy - 6f, cx + 7f, cy);
                eyePath.QuadTo(cx, cy + 6f, cx - 7f, cy);
                canvas.DrawPath(eyePath, iconStroke);
            }
            canvas.DrawCircle(cx, cy, 2f, iconFill);

            canvas.DrawRoundRect(destinationFileEditRect, 3f, 3f, btnBg);
            canvas.DrawRoundRect(destinationFileEditRect, 3f, 3f, btnBorder);
            if (iconTypeface != null) {
                using var iconFont = new SKFont(iconTypeface, 12f);
                canvas.DrawText("\uE3C9", destinationFileEditRect.MidX, destinationFileEditRect.MidY + 4f, SKTextAlign.Center, iconFont, btnTextPaint);
            }
        }

        SKRect labelClipRect = new SKRect(0, 0, destinationFileEditRect.Left - 4f, HeaderHeight);
        string destLabelText = !string.IsNullOrWhiteSpace(_destinationPath) ? $"Destination: {Path.GetFileName(_destinationPath)}" : "Destination Document";

        canvas.Save();
        canvas.ClipRect(labelClipRect);
        using (var headerTextPaint = new SKPaint { Color = new SKColor(50, 65, 85), IsAntialias = true }) {
            canvas.DrawText(destLabelText, 12f, 21f, _headerTitleFont, headerTextPaint);
        }
        canvas.Restore();

        SKRect clipArea = new SKRect(0, HeaderHeight, w, h);
        canvas.Save();
        canvas.ClipRect(clipArea);

        float currentY = 0f;
        if (_destinationRoot != null) {
            maxDestinationContentHeight = RenderTreeElement(canvas, _destinationRoot, 15f, ref currentY, 0, "");
        }
        canvas.Restore();

        float viewableHeight = h - HeaderHeight;
        if (maxDestinationContentHeight > viewableHeight) {
            float scrollbarWidth = 5f;
            float trackLeft = w - scrollbarWidth - 4f;
            float trackTop = HeaderHeight + 4f;
            float trackHeight = viewableHeight - 8f;

            float viewRatio = trackHeight / maxDestinationContentHeight;
            float thumbHeight = Math.Max(25f, trackHeight * viewRatio);
            float scrollMaxVirtual = maxDestinationContentHeight - trackHeight;
            float scrollPercent = scrollMaxVirtual <= 0 ? 0 : (-destinationVirtualScrollY / scrollMaxVirtual);
            float thumbTop = trackTop + ((trackHeight - thumbHeight) * scrollPercent);

            using var trackPaint = new SKPaint { Color = new SKColor(220, 225, 235, 120), Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawRoundRect(new SKRect(trackLeft, trackTop, trackLeft + scrollbarWidth, trackTop + trackHeight), 2.5f, 2.5f, trackPaint);

            using var thumbPaint = new SKPaint { Color = new SKColor(150, 165, 180, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawRoundRect(new SKRect(trackLeft, thumbTop, trackLeft + scrollbarWidth, thumbTop + thumbHeight), 2.5f, 2.5f, thumbPaint);
        }
    }

    private float RenderTreeElement(SKCanvas canvas, SchemaNode node, float currentX, ref float currentY, int currentDepth = 0, string parentPath = "") {
        using var font = new SKFont(SKTypeface.Default, 12f);
        using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var nodeBoxPaint = new SKPaint { Color = new SKColor(220, 230, 242), Style = SKPaintStyle.Fill };
        using var nodeBorderPaint = new SKPaint { Color = new SKColor(120, 150, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

        float nodeHeight = 22f;
        float indentSpacing = 15f;
        float totalBranchHeight = nodeHeight + 4f;

        string currentPath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath}/{node.Name}";
        float contentCenterY = currentY + (nodeHeight / 2f);
        node.LastRenderedY = contentCenterY;
        node.LastRenderedHeight = nodeHeight;
        _destinationNodeYCache[currentPath] = contentCenterY;

        float screenRowTop = HeaderHeight + 8f + destinationVirtualScrollY + currentY;
        SKRect rowRect = new SKRect(4f, screenRowTop, Width - 12f, screenRowTop + nodeHeight);

        canvas.Save();
        canvas.ClipRect(rowRect);
        canvas.DrawRoundRect(rowRect, 2f, 2f, nodeBoxPaint);
        canvas.DrawRoundRect(rowRect, 2f, 2f, nodeBorderPaint);

        string expandGlyph = node.Children.Count > 0 ? (node.IsExpanded ? "▼ " : "► ") : "  ";
        float textStartX = 4f + 6f + (currentDepth * indentSpacing);
        canvas.DrawText($"{expandGlyph}{node.Name}", textStartX, screenRowTop + 16f, font, textPaint);
        canvas.Restore();

        currentY += nodeHeight + 4f;

        if (node.IsExpanded) {
            foreach (var child in node.Children) {
                totalBranchHeight += RenderTreeElement(canvas, child, currentX, ref currentY, currentDepth + 1, currentPath);
            }
        }
        return totalBranchHeight;
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        float viewableHeight = Height - HeaderHeight;

        // Ensure header interactions are checked against the top HeaderHeight band
        if (e.Y <= HeaderHeight) {
            if (destinationFileViewRect.Contains(e.X, e.Y)) {
                OpenDestinationViewer();
                return;
            }
            if (destinationFileEllipsisRect.Contains(e.X, e.Y)) {
                BrowseDestinationFile();
                return;
            }
            if (destinationFileEditRect.Contains(e.X, e.Y)) {
                EditDestinationSchema();
                return;
            }
            return;
        }

        if (maxDestinationContentHeight > viewableHeight) {
            float scrollbarWidth = 5f;
            float trackLeft = Width - scrollbarWidth - 4f;
            float trackTop = HeaderHeight + 4f;
            float trackHeight = viewableHeight - 8f;
            SKRect trackRect = new SKRect(trackLeft, trackTop, trackLeft + scrollbarWidth, trackTop + trackHeight);
            if (trackRect.Contains(e.X, e.Y)) {
                isDraggingDestinationScrollbar = true;
                scrollbarDragStartY = e.Y;
                scrollbarDragStartScrollY = destinationVirtualScrollY;
                Capture = true;
                return;
            }
        }

        float contentMouseY = e.Y - (HeaderHeight + 8f + destinationVirtualScrollY);
        if (_destinationRoot != null) {
            float traversalY = 0f;
            if (TryToggleNodeExpansion(_destinationRoot, contentMouseY, e.X, 4f, 0, ref traversalY)) {
                Invalidate();
                this.Parent?.Invalidate();
                return;
            }

            string matchedPath = string.Empty;
            if (FindNodePathAtPosition(_destinationRoot, contentMouseY, "", ref matchedPath)) {
                NodeClicked?.Invoke(this, matchedPath);
            }
        }
    }

    private bool TryToggleNodeExpansion(SchemaNode node, float contentMouseY, float mouseX, float treeLeftX, int currentDepth, ref float currentY) {
        float nodeHeight = 22f;
        float rowHeight = nodeHeight + 4f;

        if (contentMouseY >= currentY && contentMouseY <= currentY + nodeHeight) {
            float indentSpacing = 15f;
            float glyphHitLeft = treeLeftX + 6f + (currentDepth * indentSpacing);
            float glyphHitRight = glyphHitLeft + 20f;

            if (node.Children != null && node.Children.Count > 0 && mouseX >= glyphHitLeft && mouseX <= glyphHitRight) {
                node.IsExpanded = !node.IsExpanded;
                return true;
            }
            return false;
        }

        currentY += rowHeight;
        if (node.IsExpanded && node.Children != null) {
            foreach (var child in node.Children) {
                if (TryToggleNodeExpansion(child, contentMouseY, mouseX, treeLeftX, currentDepth + 1, ref currentY)) return true;
            }
        }
        return false;
    }

    private static bool FindNodePathAtPosition(SchemaNode node, float mouseY, string parentPath, ref string foundPath) {
        string currentPath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath}/{node.Name}";
        float halfHeight = (node.LastRenderedHeight > 0 ? node.LastRenderedHeight : 22f) / 2f;

        if (node.IsExpanded && node.Children != null) {
            foreach (var child in node.Children) {
                if (FindNodePathAtPosition(child, mouseY, currentPath, ref foundPath)) return true;
            }
        }

        float tolerance = halfHeight + 2f;
        if (mouseY >= node.LastRenderedY - tolerance && mouseY <= node.LastRenderedY + tolerance) {
            foundPath = currentPath;
            return true;
        }
        return false;
    }

   

    private void BrowseDestinationFile() {
        using var ofd = new OpenFileDialog { Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*", Title = "Select Destination Target XML Schema" };
        if (ofd.ShowDialog() == DialogResult.OK) {
            _destinationPath = ofd.FileName;
            DestinationPathChanged?.Invoke(this, ofd.FileName);
            try {
                _destinationRoot = MainForm.LoadSchemaFromFile(ofd.FileName);
                DestinationSchemaReloaded?.Invoke(this, EventArgs.Empty);
            } catch (Exception ex) {
                MessageBox.Show($"Failed to load schema:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _destinationRoot = null;
            }
            Invalidate();
        }
    }

  
    private void OpenDestinationViewer() {
        _destinationPath = MainForm.DestinationPath;
        if (!string.IsNullOrEmpty(_destinationPath) && File.Exists(_destinationPath)) {
            using var viewerForm = new OutputViewerForm(_destinationPath, "Target schema");
            viewerForm.ShowDialog(this);
        } else {
            MessageBox.Show("Destination file path is not set or file does not exist.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void EditDestinationSchema() {
        _destinationPath = MainForm.DestinationPath;
        if (!string.IsNullOrEmpty(_destinationPath) && File.Exists(_destinationPath)) {
            string xmlContent = File.ReadAllText(_destinationPath);
            using var editor = new ScriptEditorDialog(xmlContent, "Destination XML Schema");
            if (editor.ShowDialog() == DialogResult.OK) {
                File.WriteAllText(_destinationPath, editor.ScriptContent);
                _destinationRoot = MainForm.LoadSchemaFromFile(_destinationPath);
                DestinationSchemaReloaded?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        } else {
            MessageBox.Show("Destination file path is not set or file does not exist.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}