using OopFactory.X12.Parsing;
using OopFactory.X12.Parsing.Model;
using SkiaMapper.Forms;
using SkiaMapper.Models;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
namespace SkiaMapper.Controls {
    public class SourceSchemaControl : SKControl {
        #region Events & Callbacks
        public event EventHandler<string>? SourcePathChanged;
        public event EventHandler<string>? LogMessageRequested;
        public event EventHandler? SourceSchemaReloaded;

        // Event raised when the user starts dragging a connection wire from a source tree node
        public event EventHandler<(string NodePath, SKPoint ScreenPoint)>? NodeDragStarted;
        #endregion
        #region Fields & Properties
        private SchemaNode? _sourceRoot;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SchemaNode? SourceRoot {
            get => _sourceRoot;
            set {
                _sourceRoot = value;
                _sourceNodeYCache.Clear();
                Invalidate();
            }
        }

        private string _sourcePath = string.Empty;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SourcePath {
            get => _sourcePath;
            set {
                _sourcePath = value;
                Invalidate();
            }
        }

        private float sourceVirtualScrollY = 0f;
        private float maxSourceContentHeight = 0f;
        private bool isDraggingSourceScrollbar = false;
        private float scrollbarDragStartY = 0f;
        private float scrollbarDragStartScrollY = 0f;

        // Caching rendered node vertical coordinates
        private readonly Dictionary<string, float> _sourceNodeYCache = [];

        // Hit-test rectangles
        private SKRect sourceFileEllipsisRect;
        private SKRect sourceFileViewRect;
        private SKRect sourceFileEditRect;
        private const float HeaderHeight = 32f;

        // Fonts & Paints
        private readonly SKFont _headerButtonFont = new(SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 12f);
        private readonly SKFont _headerTitleFont = new(SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 11f);
        private SKTypeface? iconTypeface;
        #endregion
        public SourceSchemaControl() {
            this.DoubleBuffered = true;
            this.Dock = DockStyle.Fill;
            this.SetStyle(ControlStyles.Selectable, true);

            this.MouseWheel += SourceSchemaControl_MouseWheel;

            // Load icon typeface if available
            try {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string resourceName = "SkiaMapper.MaterialIcons-Regular.ttf";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null) {
                    iconTypeface = SKTypeface.FromStream(stream);
                }
            } catch {
                // Fallback if font resource is missing
            }
        }
        public float? GetNodeVisualY(string nodePath) {
            if (string.IsNullOrEmpty(nodePath)) return null;
            if (_sourceNodeYCache.TryGetValue(nodePath, out float contentY)) {
                float headerHeight = 32f;
                return headerHeight + 8f + sourceVirtualScrollY + contentY;
            }
            return null;
        }
        private void SourceSchemaControl_MouseWheel(object? sender, MouseEventArgs e) {
            float viewableHeight = Height - HeaderHeight;
            if (maxSourceContentHeight <= viewableHeight) {
                sourceVirtualScrollY = 0f;
                Invalidate();
                return;
            }

            int scrollDelta = e.Delta > 0 ? 30 : -30;
            sourceVirtualScrollY += scrollDelta;
            float minScrollY = -(maxSourceContentHeight - viewableHeight);
            sourceVirtualScrollY = Math.Clamp(sourceVirtualScrollY, minScrollY, 0f);
            Invalidate();
            this.Parent?.Invalidate();
        }
        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);
            _sourceNodeYCache.Clear();

            var canvas = e.Surface.Canvas;
            int w = e.Info.Width;
            int h = e.Info.Height;
            canvas.Clear(SKColors.GhostWhite);

            // 1. Draw Header Background & Border
            SKRect leftHeaderRect = new SKRect(0, 0, w, HeaderHeight);
            using (var headerBg = new SKPaint { Color = new SKColor(230, 235, 242), Style = SKPaintStyle.Fill, IsAntialias = true }) {
                canvas.DrawRect(leftHeaderRect, headerBg);
            }
            using (var headerBorder = new SKPaint { Color = new SKColor(180, 190, 205), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true }) {
                canvas.DrawLine(0, HeaderHeight, w, HeaderHeight, headerBorder);
            }

            // 2. Setup Header Action Buttons (Ellipsis, View, Edit)
            float btnWidth = 24f;
            float btnHeight = 20f;
            sourceFileEllipsisRect = new SKRect(w - btnWidth - 6f, (HeaderHeight - btnHeight) / 2f, w - 6f, (HeaderHeight + btnHeight) / 2f);
            sourceFileViewRect = new SKRect(sourceFileEllipsisRect.Left - btnWidth - 4f, sourceFileEllipsisRect.Top, sourceFileEllipsisRect.Left - 4f, sourceFileEllipsisRect.Bottom);
            sourceFileEditRect = new SKRect(sourceFileViewRect.Left - btnWidth - 4f, sourceFileViewRect.Top, sourceFileViewRect.Left - 4f, sourceFileViewRect.Bottom);

            using (var btnBg = new SKPaint { Color = new SKColor(210, 220, 235), Style = SKPaintStyle.Fill, IsAntialias = true })
            using (var btnBorder = new SKPaint { Color = new SKColor(160, 175, 195), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true })
            using (var btnTextPaint = new SKPaint { Color = new SKColor(40, 50, 70), IsAntialias = true })
            using (var iconStroke = new SKPaint { Color = new SKColor(40, 50, 70), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true })
            using (var iconFill = new SKPaint { Color = new SKColor(40, 50, 70), Style = SKPaintStyle.Fill, IsAntialias = true }) {

                // Ellipsis button (...)
                canvas.DrawRoundRect(sourceFileEllipsisRect, 3f, 3f, btnBg);
                canvas.DrawRoundRect(sourceFileEllipsisRect, 3f, 3f, btnBorder);
                canvas.DrawText("...", sourceFileEllipsisRect.MidX, sourceFileEllipsisRect.MidY + 4f, SKTextAlign.Center, _headerButtonFont, btnTextPaint);

                // View button (Eye icon)
                canvas.DrawRoundRect(sourceFileViewRect, 3f, 3f, btnBg);
                canvas.DrawRoundRect(sourceFileViewRect, 3f, 3f, btnBorder);
                float cx1 = sourceFileViewRect.MidX, cy1 = sourceFileViewRect.MidY;
                using (var eyePath = new SKPath()) {
                    eyePath.MoveTo(cx1 - 7f, cy1);
                    eyePath.QuadTo(cx1, cy1 - 6f, cx1 + 7f, cy1);
                    eyePath.QuadTo(cx1, cy1 + 6f, cx1 - 7f, cy1);
                    canvas.DrawPath(eyePath, iconStroke);
                }
                canvas.DrawCircle(cx1, cy1, 2f, iconFill);

                // Edit button (Pencil icon)
                canvas.DrawRoundRect(sourceFileEditRect, 3f, 3f, btnBg);
                canvas.DrawRoundRect(sourceFileEditRect, 3f, 3f, btnBorder);
                if (iconTypeface != null) {
                    using var iconFont = new SKFont(iconTypeface, 12f);
                    canvas.DrawText("\uE3C9", sourceFileEditRect.MidX, sourceFileEditRect.MidY + 4f, SKTextAlign.Center, iconFont, btnTextPaint);
                }
            }

            // Header Label Text
            SKRect labelClipRect = new SKRect(0, 0, sourceFileEditRect.Left - 4f, HeaderHeight);
            string sourceLabelText = !string.IsNullOrWhiteSpace(_sourcePath) ? $"Source: {Path.GetFileName(_sourcePath)}" : "Source Document";

            canvas.Save();
            canvas.ClipRect(labelClipRect);
            using (var headerTextPaint = new SKPaint { Color = new SKColor(50, 65, 85), IsAntialias = true }) {
                canvas.DrawText(sourceLabelText, 12f, 21f, _headerTitleFont, headerTextPaint);
            }
            canvas.Restore();

            // 3. Render Tree Nodes with Clipping & Virtual Scroll
            SKRect sourceClipArea = new SKRect(0, HeaderHeight, w, h);
            canvas.Save();
            canvas.ClipRect(sourceClipArea);

            float currentY = 0f;
            if (_sourceRoot != null) {
                maxSourceContentHeight = RenderTreeElement(canvas, _sourceRoot, 15f, ref currentY, 0, "");
            }
            canvas.Restore();

            // 4. Draw Scrollbar if content overflows
            float viewableHeight = h - HeaderHeight;
            if (maxSourceContentHeight > viewableHeight) {
                float scrollbarWidth = 5f;
                float trackLeft = w - scrollbarWidth - 4f;
                float trackTop = HeaderHeight + 4f;
                float trackHeight = viewableHeight - 8f;

                float viewRatio = trackHeight / maxSourceContentHeight;
                float thumbHeight = Math.Max(25f, trackHeight * viewRatio);
                float scrollMaxVirtual = maxSourceContentHeight - trackHeight;
                float scrollPercent = scrollMaxVirtual <= 0 ? 0 : (-sourceVirtualScrollY / scrollMaxVirtual);
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
            _sourceNodeYCache[currentPath] = contentCenterY;

            float screenRowTop = HeaderHeight + 8f + sourceVirtualScrollY + currentY;
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

            // 1. Header Button Actions
            if (e.Y <= HeaderHeight) {
                if (sourceFileViewRect.Contains(e.X, e.Y)) {
                    OpenSourceViewer();
                    return;
                }
                if (sourceFileEllipsisRect.Contains(e.X, e.Y)) {
                    BrowseSourceFile();
                    return;
                }
                if (sourceFileEditRect.Contains(e.X, e.Y)) {
                    EditSourceSchema();
                    return;
                }
                return;
            }

            // 2. Scrollbar Dragging
            if (maxSourceContentHeight > viewableHeight) {
                float scrollbarWidth = 5f;
                float trackLeft = Width - scrollbarWidth - 4f;
                float trackTop = HeaderHeight + 4f;
                float trackHeight = viewableHeight - 8f;
                SKRect trackRect = new SKRect(trackLeft, trackTop, trackLeft + scrollbarWidth, trackTop + trackHeight);
                if (trackRect.Contains(e.X, e.Y)) {
                    isDraggingSourceScrollbar = true;
                    scrollbarDragStartY = e.Y;
                    scrollbarDragStartScrollY = sourceVirtualScrollY;
                    Capture = true;
                    return;
                }
            }

            // 3. Tree Chevron Toggling & Wire Drag Initiation
            float contentMouseY = e.Y - (HeaderHeight + 8f + sourceVirtualScrollY);
            if (_sourceRoot != null) {
                float traversalY = 0f;
                if (TryToggleNodeExpansion(_sourceRoot, contentMouseY, e.X, 4f, 0, ref traversalY)) {
                    Invalidate();
                    return;
                }

                // Check if clicked node row to start dragging a wire
                string matchedPath = string.Empty;
                if (FindNodePathAtPosition(_sourceRoot, contentMouseY, "", ref matchedPath)) {
                    if (_sourceNodeYCache.TryGetValue(matchedPath, out float contentY)) {
                        float screenY = HeaderHeight + 8f + sourceVirtualScrollY + contentY;
                        NodeDragStarted?.Invoke(this, (matchedPath, new SKPoint(Width, screenY)));
                       this.Parent.Capture = true;
                    }
                }
            }
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if (isDraggingSourceScrollbar) {
                float viewableHeight = Height - HeaderHeight;
                float trackHeight = viewableHeight - 8f;
                float scrollMaxVirtual = maxSourceContentHeight - trackHeight;
                float deltaY = e.Y - scrollbarDragStartY;
                float scrollableTrack = trackHeight - 25f; // assuming min thumb height
                if (scrollableTrack > 0) {
                    float ratio = scrollMaxVirtual / scrollableTrack;
                    sourceVirtualScrollY = Math.Clamp(scrollbarDragStartScrollY - (deltaY * ratio), -scrollMaxVirtual, 0f);
                    Invalidate();
                }
            }
        }
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);
            if (isDraggingSourceScrollbar) {
                isDraggingSourceScrollbar = false;
                Capture = false;
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
                    if (TryToggleNodeExpansion(child, contentMouseY, mouseX, treeLeftX, currentDepth + 1, ref currentY)) {
                        return true;
                    }
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
        private void OpenSourceViewer() {
            if (!string.IsNullOrEmpty(_sourcePath) && File.Exists(_sourcePath)) {
                using var viewerForm = new OutputViewerForm(_sourcePath, "Input schema");
                viewerForm.ShowDialog(this);
            } else {
                MessageBox.Show("Source file path is not set or file does not exist.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private void BrowseSourceFile() {
            using var ofd = new OpenFileDialog { Filter = "Any extension |*.*", Title = "Select Source Schema or EDI File" };
            if (ofd.ShowDialog() == DialogResult.OK) {
                string filePath = ofd.FileName;
                if (Path.GetExtension(filePath).ToLower() is ".edi" or ".txt") {
                    X12Parser parser = new();
                    using Stream reader = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    var interchanges = parser.ParseMultiple(reader);
                    string xml = interchanges.First().Serialize();
                    filePath = Path.ChangeExtension(filePath, ".xml");
                    File.WriteAllText(filePath, xml);
                }

                _sourcePath = filePath;
                SourcePathChanged?.Invoke(this, filePath);
                try {
                    _sourceRoot = MainForm.LoadSchemaFromFile(filePath);
                    SourceSchemaReloaded?.Invoke(this, EventArgs.Empty);
                } catch (Exception ex) {
                    MessageBox.Show($"Failed to load source schema tree:\n{ex.Message}", "Schema Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _sourceRoot = null;
                }
                Invalidate();
            }
        }
        private void EditSourceSchema() {
            if (!string.IsNullOrEmpty(_sourcePath) && File.Exists(_sourcePath)) {
                string xmlContent = File.ReadAllText(_sourcePath);
                using var editor = new ScriptEditorDialog(xmlContent, "Source XML Schema");
                if (editor.ShowDialog() == DialogResult.OK) {
                    File.WriteAllText(_sourcePath, editor.ScriptContent);
                    _sourceRoot = MainForm.LoadSchemaFromFile(_sourcePath);
                    SourceSchemaReloaded?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }
        public float? GetNodeWorkspaceY(string nodePath) {
            if (string.IsNullOrEmpty(nodePath)) return null;

            // _sourceNodeYCache stores contentCenterY recorded during RenderTreeElement
            if (_sourceNodeYCache.TryGetValue(nodePath, out float contentY)) {
                // Must match the exact vertical layout math used in OnPaintSurface:
                // HeaderHeight (32f) + top padding (8f) + virtual scroll offset + content relative Y
                return 32f + 8f + sourceVirtualScrollY + contentY;
            }
            return null;
        }
    }
}