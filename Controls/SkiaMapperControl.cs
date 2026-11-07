using OopFactory.X12.Parsing;
using OopFactory.X12.Parsing.Model;
using SkiaMapper.Forms;
using SkiaMapper.Models;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Xsl;

namespace SkiaMapper.Controls;

public class SkiaMapperControl : SKControl {
    #region Fields and Properties
    public enum PaintArea {
        LeftPanel,
        MiddlePanel,
        RightPanel,
        LogPanel
    }

    // --- Segregated Sub-Control ---
    public SourceSchemaControl SourcePanelControl { get; private set; }
    public DestinationSchemaControl DestinationPanelControl { get; private set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SchemaNode? DestinationRoot {
        get => DestinationPanelControl?.DestinationRoot;
        set {
            if (DestinationPanelControl != null) DestinationPanelControl.DestinationRoot = value;
        }
    }

    private readonly List<string> logMessages = new();

    public void LogMessage(string message, [CallerMemberName] string cmn = null, [CallerLineNumber] int ln = 0) {
        logMessages.Add($"[{DateTime.Now:HH:mm:ss}]:{cmn}:{ln}  {message} ");
        if (logMessages.Count > 100) logMessages.RemoveAt(0); // Keep last 100 entries
        Invalidate();
    }

    // Destination Tree Virtual Scroll State
    private float destinationVirtualScrollY = 0f;
    private float maxDestinationContentHeight = 0f;
    private bool isDraggingDestinationScrollbar = false;

    // Panel Dimension Metrics (Splitters)
    private float leftTreeWidth = 280f;
    private float rightTreeWidth = 280f;
    private float logPanelHeight = 120f;
    private const float SplitterWidth = 5f;

    // Tool Palette Window Metrics
    private SKRect paletteBounds = new(310f, 30f, 550f, 450f);
    private const float PaletteHeaderHeight = 64f;
    private bool isDraggingPalette = false;
    private PointF paletteDragOffset;
    private bool isPaletteExpanded = true;
    private readonly float expandedPaletteHeight = 420f;

    // Drag-and-Drop / Interactive Link State Tracking
    private FunctoidDefinition? draggingPaletteFunctoid = null;
    private PointF paletteItemDragOffset;
    private Dictionary<FunctoidDefinition, SKRect> renderedItemHitboxes = new();
    private FunctoidInstance? draggingCanvasInstance = null;
    private PointF canvasInstanceDragOffset;

    // Active Splitter/Line Drag Tracking
    private bool isResizingLog = false;
    private bool isResizingLeft = false;
    private bool isResizingRight = false;
    private bool isCanvasDirty = false;
    private PointF lastMousePos;

    // Header Button hit boxes
    private SKRect saveBtnRect;
    private SKRect loadBtnRect;
    private SKRect clearBtnRect;
    private SKRect executeXsltBtnRect;
    private SKRect xsltFileEllipsisRect;
    private SKRect showOutputCheckboxRect;
    private SKRect destinationFileEditRect;
    private SKRect destinationFileViewRect;
    private SKRect destinationFileEllipsisRect;

    private bool showOutputChecked = true;

    private ContextMenuStrip functoidContextMenu;
    private ContextMenuStrip connectionContextMenu;
    private FunctoidInstance? contextTargetInstance = null;
    private FunctoidInstance? selectedCanvasInstance = null;
    private MappingConnection? selectedConnection = null;
    private MappingConnection? contextTargetConnection = null;

    private float toolboxVirtualScrollY = 0f;
    private float maxToolboxContentHeight = 0f;

#pragma warning disable CS0414
    private bool isDraggingToolboxScrollbar = false;
#pragma warning restore CS0414

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SchemaNode? SourceRoot {
        get => SourcePanelControl?.SourceRoot;
        set {
            if (SourcePanelControl != null) SourcePanelControl.SourceRoot = value;
        }
    }



    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<FunctoidCategory> FunctoidCategories { get; set; } = [];

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<FunctoidDefinition> AvailableFunctoids { get; set; } = new();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ObservableCollection<FunctoidInstance> ActiveFunctoids { get; } = [];

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ObservableCollection<MappingConnection> Connections { get; } = [];

    private ConnectionEndpoint? dragSource = null;
    private PointF currentDragPoint;

    // Maps destination node paths string to its exact Y-coordinate
    private readonly Dictionary<string, float> _destinationNodeYCache = [];

    private readonly SKFont _headerButtonFont = new(SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 12f);
    private readonly SKFont _headerTitleFont = new(SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 11f);
    private readonly SKFont _ghostFont = new(SKTypeface.Default, 12f);

    private SKTypeface iconTypeface;
    private SKFont iconFont;

    #endregion Properties and Fields

    public SkiaMapperControl() {
        this.DoubleBuffered = true;
        this.Dock = DockStyle.Fill;
        this.SetStyle(ControlStyles.Selectable, true);

        // 1. Initialize Context Menus
        functoidContextMenu = new ContextMenuStrip();
        var editScriptItem = new ToolStripMenuItem("Configure Script Block...");
        editScriptItem.Click += EditScriptItem_Click;
        functoidContextMenu.Items.Add(editScriptItem);

        var duplicateFunctoidItem = new ToolStripMenuItem("Duplicate Functoid");
        duplicateFunctoidItem.Click += DuplicateFunctoidItem_Click;
        functoidContextMenu.Items.Add(duplicateFunctoidItem);

        var deleteFunctoidItem = new ToolStripMenuItem("Delete Functoid");
        deleteFunctoidItem.Click += DeleteFunctoidItem_Click;
        functoidContextMenu.Items.Add(deleteFunctoidItem);

        connectionContextMenu = new ContextMenuStrip();
        var deleteConnItem = new ToolStripMenuItem("Delete Connection Wire");
        deleteConnItem.Click += DeleteConnectionItem_Click;
        connectionContextMenu.Items.Add(deleteConnItem);

        // 2. Mouse Wheel and Change Events
        this.MouseWheel += SkiaMapperControl_MouseWheel;
        ActiveFunctoids.CollectionChanged += OnCanvasStructureChanged;
        Connections.CollectionChanged += OnCanvasStructureChanged;

        // 3. Load Embedded Icon Font
        try {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = "SkiaMapper.MaterialIcons-Regular.ttf";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null) {
                iconTypeface = SKTypeface.FromStream(stream);
                iconFont = new SKFont(iconTypeface, 14f);
            }
        } catch {
            // Fallback font handling
        }

        // 4. Instantiate & Wire Up Segregated Source Panel Control
        InitializeSourcePanelControl();
        InitializeDestinationPanelControl();
    }

    private void InitializeSourcePanelControl() {
        SourcePanelControl = new SourceSchemaControl {
            Dock = DockStyle.None,
            Width = (int)leftTreeWidth
        };

        // Wire up callbacks
        SourcePanelControl.NodeDragStarted += (s, args) => {
            dragSource = new ConnectionEndpoint {
                Type = ConnectionEndpointType.SourceNode,
                NodePath = args.NodePath
            };
            currentDragPoint = new PointF(args.ScreenPoint.X, args.ScreenPoint.Y);
            this.Capture = true;
            Invalidate();
        };

        SourcePanelControl.SourcePathChanged += (s, path) => {
            MainForm.SourcePath = path;
            isCanvasDirty = false;
        };

        SourcePanelControl.SourceSchemaReloaded += (s, args) => {
            ActiveFunctoids.Clear();
            Connections.Clear();
            dragSource = null;
            draggingCanvasInstance = null;
            selectedCanvasInstance = null;
            selectedConnection = null;
            contextTargetInstance = null;
            contextTargetConnection = null;
            isCanvasDirty = false;
            Invalidate();
        };

        this.Controls.Add(SourcePanelControl);
    }

    private void InitializeDestinationPanelControl() {
        DestinationPanelControl = new DestinationSchemaControl {
            Dock = DockStyle.None
        };

        DestinationPanelControl.DestinationPathChanged += (s, path) => {
            MainForm.DestinationPath = path;
            isCanvasDirty = false;
        };

        DestinationPanelControl.DestinationSchemaReloaded += (s, args) => {
            ActiveFunctoids.Clear();
            Connections.Clear();
            dragSource = null;
            draggingCanvasInstance = null;
            selectedCanvasInstance = null;
            selectedConnection = null;
            contextTargetInstance = null;
            contextTargetConnection = null;
            isCanvasDirty = false;
            Invalidate();
        };

        this.Controls.Add(DestinationPanelControl);
    }

    protected override void OnLayout(LayoutEventArgs levent) {
        base.OnLayout(levent);
        if (SourcePanelControl != null) {
            SourcePanelControl.Location = new System.Drawing.Point(0, 0);
            SourcePanelControl.Size = new System.Drawing.Size((int)leftTreeWidth, (int)(Height - logPanelHeight));
        }
        if (DestinationPanelControl != null) {
            int rightLeft = (int)(Width - rightTreeWidth);
            DestinationPanelControl.Location = new System.Drawing.Point(rightLeft, 0);
            DestinationPanelControl.Size = new System.Drawing.Size((int)rightTreeWidth, (int)(Height - logPanelHeight));
        }
    }

    private void OnCanvasStructureChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
        isCanvasDirty = true;
    }

    private void BtnExecuteTransform_Click(object? sender, EventArgs e) {
        try {
            var generator = new SkiaMapper.Services.XsltMapGenerator(this.ActiveFunctoids, this.Connections);
            generator.GenerateAndSaveMap();

            var xslt = new XslCompiledTransform();
            using (var reader = XmlReader.Create(MainForm.XsltPath)) {
                xslt.Load(reader);
            }

            var args = new XsltArgumentList();
            object extensionInstance = generator.BuildDynamicExtensionObject();
            args.AddExtensionObject("urn:my-scripts", extensionInstance);

            using (var xmlReader = XmlReader.Create(MainForm.SourcePath))
            using (var xmlWriter = XmlWriter.Create("POoutput.xml", xslt.OutputSettings)) {
                xslt.Transform(xmlReader, args, xmlWriter);
            }

            if (showOutputChecked) {
                try {
                    using (var viewerForm = new SkiaMapper.Forms.OutputViewerForm("POoutput.xml", "Transformed output")) {
                        viewerForm.ShowDialog(this);
                    }
                } catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"Failed to open output viewer form: {ex.Message}");
                }
            } else {
                AutoFadeMessageBox.Show("XSLT transformation executed successfully!", "Execution Complete", 5000);
            }
        } catch (Exception ex) {
            ErrorHelper.HandleException(ex, "parsing error");
            AutoFadeMessageBox.Show("Error during xslt transformation, Error copied to clipboard", "failed", 5000);
        }
    }

    private void SkiaMapperControl_MouseWheel(object? sender, MouseEventArgs e) {
        float mainContentHeight = Height - logPanelHeight;
        float centerLeft = leftTreeWidth;
        float centerRight = Width - rightTreeWidth;

        if (e.X >= centerLeft && e.X <= centerRight && e.Y < mainContentHeight) {
            FunctoidInstance? hoveredFunctoid = ActiveFunctoids.LastOrDefault(f => {
                float absX = centerLeft + f.X;
                return e.X >= absX && e.X <= absX + f.Width &&
                       e.Y >= f.Y && e.Y <= f.Y + f.Height;
            });

            if (hoveredFunctoid != null) {
                float scaleFactor = e.Delta > 0 ? 1.05f : 0.95f;
                hoveredFunctoid.Width = Math.Clamp(hoveredFunctoid.Width * scaleFactor, 80f, 300f);
                hoveredFunctoid.Height = Math.Clamp(hoveredFunctoid.Height * scaleFactor, 40f, 150f);
                Invalidate();
                return;
            }
        }

        if (isPaletteExpanded && paletteBounds.Contains(e.X, e.Y)) {
            float viewableHeight = paletteBounds.Height - PaletteHeaderHeight - 12f;
            maxToolboxContentHeight = CalculateTotalToolboxContentHeight();

            if (maxToolboxContentHeight <= viewableHeight) {
                toolboxVirtualScrollY = 0f;
                Invalidate();
                return;
            }

            int scrollDelta = e.Delta > 0 ? 30 : -30;
            toolboxVirtualScrollY += scrollDelta;
            float minScrollY = -(maxToolboxContentHeight - viewableHeight);
            toolboxVirtualScrollY = Math.Clamp(toolboxVirtualScrollY, minScrollY, 0f);
            Invalidate();
            return;
        }

        if (e.X > (Width - rightTreeWidth) && e.Y < mainContentHeight && e.Y > 32f) {
            float viewableHeight = mainContentHeight - 32f;
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
            return;
        }
    }

    private float CalculateTotalToolboxContentHeight() {
        float itemHeight = 24f;
        float categoryHeaderHeight = 22f;
        float totalHeight = 0f;

        foreach (var category in FunctoidCategories) {
            totalHeight += categoryHeaderHeight + 4f;
            if (category.IsExpanded) {
                foreach (var functoid in AvailableFunctoids) {
                    if (functoid.CategoryId != category.Id) continue;
                    totalHeight += itemHeight + 4f;
                }
            }
            totalHeight += 6f;
        }
        return totalHeight;
    }

    private void DeleteFunctoidItem_Click(object? sender, EventArgs e) {
        if (contextTargetInstance != null) {
            string targetId = contextTargetInstance.Id.ToString();
            var connectionsToRemove = Connections.Where(c =>
                (c.Source?.Type == ConnectionEndpointType.Functoid && c.Source.FunctoidInstanceId?.ToString() == targetId) ||
                (c.Target?.Type == ConnectionEndpointType.Functoid && c.Target.FunctoidInstanceId?.ToString() == targetId)
            ).ToArray();

            foreach (var connection in connectionsToRemove) {
                Connections.Remove(connection);
            }
            ActiveFunctoids.Remove(contextTargetInstance);

            if (selectedCanvasInstance == contextTargetInstance) {
                selectedCanvasInstance = null;
            }
            contextTargetInstance = null;
            Invalidate();
        }
    }

    private void DeleteConnectionItem_Click(object? sender, EventArgs e) {
        if (contextTargetConnection != null) {
            Connections.Remove(contextTargetConnection);
            if (selectedConnection == contextTargetConnection) {
                selectedConnection = null;
            }
            contextTargetConnection = null;
            this.Invalidate();
        }
    }

    private void EditScriptItem_Click(object? sender, EventArgs e) {
        if (contextTargetInstance != null) {
            using var editor = new ScriptEditorDialog(contextTargetInstance, this);
            if (editor.ShowDialog() == DialogResult.OK) {
                isCanvasDirty = true;
                Invalidate();
            }
        }
    }

    private void DuplicateFunctoidItem_Click(object? sender, EventArgs e) {
        if (contextTargetInstance != null) {
            var duplicatedInstance = new FunctoidInstance {
                Id = Guid.NewGuid(),
                X = contextTargetInstance.X + 25f,
                Y = contextTargetInstance.Y + 25f,
                Width = contextTargetInstance.Width,
                Height = contextTargetInstance.Height,
                Definition = contextTargetInstance.Definition,
                CustomScriptBody = contextTargetInstance.CustomScriptBody,
                CustomMethodName = contextTargetInstance.CustomMethodName
            };

            ActiveFunctoids.Add(duplicatedInstance);
            selectedCanvasInstance = duplicatedInstance;
            contextTargetInstance = null;
            Invalidate();
        }
    }

    public int GetFunctoidInputCount(FunctoidInstance instance) {
        if (instance?.Definition == null) return 1;
        int baseCount = instance.Definition.InputParametersCount;
        bool isVariadic = instance.Definition.IsVariable;

        int maxConnectedIndex = -1;
        if (Connections != null) {
            var inputs = Connections.Where(c =>
                c.Target != null &&
                c.Target.Type == ConnectionEndpointType.Functoid &&
                c.Target.FunctoidInstanceId == instance.Id);

            if (inputs.Any()) {
                maxConnectedIndex = inputs.Max(c => c.Target.InputIndex);
            }
        }

        if (isVariadic) {
            return Math.Max(baseCount, maxConnectedIndex + 2);
        }
        return baseCount;
    }

    private float GetFunctoidInputSlotY(FunctoidInstance instance, int slotIndex) {
        int totalSlots = GetFunctoidInputCount(instance);
        float slotHeight = instance.Height / totalSlots;
        return instance.Y + (slotIndex * slotHeight) + (slotHeight / 2f);
    }

    #region Paint and Render methods

    private float RenderTreeElement(SKCanvas canvas, SchemaNode node, float currentX, ref float currentY, bool isSourceTree, int currentDepth = 0, string parentPath = "") {
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

        if (!isSourceTree) {
            _destinationNodeYCache[currentPath] = contentCenterY;
        }

        float headerHeight = 32f;
        float scrollY = destinationVirtualScrollY;
        float screenRowTop = headerHeight + 8f + scrollY + currentY;

        float absoluteLeft = (Width - rightTreeWidth) + 4f;
        float absoluteRight = Width - 4f;

        SKRect rowRect = new SKRect(absoluteLeft, screenRowTop, absoluteRight, screenRowTop + nodeHeight);

        canvas.Save();
        canvas.ClipRect(rowRect);
        canvas.DrawRoundRect(rowRect, 2f, 2f, nodeBoxPaint);
        canvas.DrawRoundRect(rowRect, 2f, 2f, nodeBorderPaint);

        string expandGlyph = node.Children.Count > 0 ? (node.IsExpanded ? "▼ " : "► ") : "  ";
        float textStartX = absoluteLeft + 6f + (currentDepth * indentSpacing);
        canvas.DrawText($"{expandGlyph}{node.Name}", textStartX, screenRowTop + 16f, font, textPaint);
        canvas.Restore();

        currentY += nodeHeight + 4f;

        if (node.IsExpanded) {
            foreach (var child in node.Children) {
                totalBranchHeight += RenderTreeElement(canvas, child, currentX, ref currentY, isSourceTree, currentDepth + 1, currentPath);
            }
        }
        return totalBranchHeight;
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
        if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime) {
            return;
        }

        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        int w = e.Info.Width;
        int h = e.Info.Height;
        canvas.Clear(SKColors.White);

        float centerLeft = leftTreeWidth;
        float centerRight = w - rightTreeWidth;
        float mainContentHeight = h - logPanelHeight;

        using var bgPaint = new SKPaint();

        // 1. Render Middle Panel Workspace Canvas
        bgPaint.Color = new SKColor(245, 247, 250);
        canvas.DrawRect(centerLeft, 0, centerRight - centerLeft, mainContentHeight, bgPaint);
        RenderActiveGridFunctoids(canvas, centerLeft);

        // 2. Render Log Panel
        bgPaint.Color = SKColors.Black;
        canvas.DrawRect(0, mainContentHeight, w, logPanelHeight, bgPaint);

        using var logFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 11f);
        using var logTextPaint = new SKPaint { Color = SKColors.Lime, IsAntialias = true };

        float logY = mainContentHeight + 18f;
        foreach (var msg in logMessages.TakeLast(5)) {
            canvas.DrawText(msg, 12f, logY, logFont, logTextPaint);
            logY += 18f;
        }

        // 3. Floating Tool Palette Window
        RenderFunctoidToolPalette(canvas);

        // 4. Layout Splitter Bars
        bgPaint.Color = SKColors.DarkGray;
        canvas.DrawRect(leftTreeWidth - 2, 0, SplitterWidth, mainContentHeight, bgPaint);
        canvas.DrawRect(centerRight - 3, 0, SplitterWidth, mainContentHeight, bgPaint);
        canvas.DrawRect(0, mainContentHeight - 2, w, SplitterWidth, bgPaint);

        // 5. Map Wire Connections
        RenderMapConnections(canvas);

        // 6. Live Dragging Connection Line
        if (dragSource != null) {
            using var dragPaint = new SKPaint { Color = SKColors.Orange, StrokeWidth = 2f, IsAntialias = true, Style = SKPaintStyle.Stroke };
            using var previewPath = new SKPath();

            SKPoint portStartPoint = GetSourceEndpointLocation(dragSource);
            if (portStartPoint.IsEmpty) {
                portStartPoint = new SKPoint((float)currentDragPoint.X, (float)currentDragPoint.Y);
            }
            previewPath.MoveTo(portStartPoint.X, portStartPoint.Y);

            SKPoint targetPoint = new SKPoint(lastMousePos.X, lastMousePos.Y);

            if (dragSource.Type != ConnectionEndpointType.DestinationNode) {
                foreach (var instance in ActiveFunctoids) {
                    float absFunctoidX = centerLeft + instance.X;
                    SKRect hitBox = new SKRect(absFunctoidX, instance.Y, absFunctoidX + instance.Width, instance.Y + instance.Height);

                    if (hitBox.Contains(lastMousePos.X, lastMousePos.Y) && lastMousePos.X <= hitBox.MidX) {
                        int maxParams = GetFunctoidInputCount(instance);
                        float relativeY = lastMousePos.Y - instance.Y;
                        float sectorHeight = instance.Height / (maxParams > 0 ? maxParams : 1);
                        int hoveredSlotIndex = (int)(relativeY / sectorHeight);

                        if (hoveredSlotIndex < 0) hoveredSlotIndex = 0;
                        if (hoveredSlotIndex >= maxParams) hoveredSlotIndex = maxParams - 1;

                        targetPoint = new SKPoint(absFunctoidX, GetFunctoidInputSlotY(instance, hoveredSlotIndex));
                        break;
                    }
                }
            }

            float controlOffset = Math.Max(30f, Math.Abs(targetPoint.X - portStartPoint.X) * 0.5f);
            float cp1X = portStartPoint.X + controlOffset;
            float cp1Y = portStartPoint.Y;
            float cp2X = targetPoint.X - controlOffset;
            float cp2Y = targetPoint.Y;

            previewPath.CubicTo(cp1X, cp1Y, cp2X, cp2Y, targetPoint.X, targetPoint.Y);
            canvas.DrawPath(previewPath, dragPaint);
        }

        // 7. Ghost Palette Drag Box
        if (draggingPaletteFunctoid != null) {
            using var ghostBoxPaint = new SKPaint { Color = new SKColor(52, 152, 219, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var ghostBorderPaint = new SKPaint { Color = new SKColor(41, 128, 185), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            using var ghostTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };

            float ghostX = lastMousePos.X - paletteItemDragOffset.X;
            float ghostY = lastMousePos.Y - paletteItemDragOffset.Y;
            SKRect ghostRect = new SKRect(ghostX, ghostY, ghostX + 110f, ghostY + 36f);

            canvas.DrawRoundRect(ghostRect, 4f, 4f, ghostBoxPaint);
            canvas.DrawRoundRect(ghostRect, 4f, 4f, ghostBorderPaint);
            canvas.DrawText(draggingPaletteFunctoid.Name, ghostRect.MidX, ghostRect.MidY + 4f, SKTextAlign.Center, _ghostFont, ghostTextPaint);
        }
    }

    private void RenderActiveGridFunctoids(SKCanvas canvas, float centerLeft) {
        using var gFont = new SKFont(SKTypeface.Default);
        using var boxPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        using var accentPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        using var borderPaint = new SKPaint { Color = new SKColor(210, 215, 220), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
        using var textPaint = new SKPaint { Color = new SKColor(50, 50, 50), IsAntialias = true };
        using var portPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var portBorder = new SKPaint { Color = new SKColor(120, 130, 140), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
        using var modifiedIndicatorPaint = new SKPaint { Color = new SKColor(231, 76, 60), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var modifiedIndicatorBorder = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };

        foreach (var instance in ActiveFunctoids) {
            float absX = centerLeft + instance.X;
            SKRect rect = new(absX, instance.Y, absX + instance.Width, instance.Y + instance.Height);

            SKColor catColor = GetFunctoidCategoryColor(instance);
            boxPaint.Color = catColor.WithAlpha(35);
            accentPaint.Color = catColor;

            canvas.DrawRoundRect(rect, 4f, 4f, boxPaint);

            float accentBarWidth = 5f;
            SKRect accentBarRect = new(rect.Left, rect.Top, rect.Left + accentBarWidth, rect.Bottom);

            canvas.Save();
            using (var clipPath = new SKPath()) {
                clipPath.AddRoundRect(rect, 4f, 4f);
                canvas.ClipPath(clipPath);
            }
            canvas.DrawRect(accentBarRect, accentPaint);
            canvas.Restore();

            canvas.DrawRoundRect(rect, 4f, 4f, borderPaint);

            string displayName = !string.IsNullOrEmpty(instance.CustomMethodName)
                ? instance.CustomMethodName
                : ExtractMethodNameFromTemplate(instance.CustomScriptBody, instance.Definition?.Name ?? "Functoid", 0);

            canvas.DrawText(displayName, rect.MidX + (accentBarWidth / 2f), rect.MidY + 4f, SKTextAlign.Center, gFont, textPaint);

            if (instance.IsScriptCustomized) {
                float dotX = rect.Right - 6f;
                float dotY = rect.Top + 6f;
                float dotRadius = 3.5f;

                canvas.DrawCircle(dotX, dotY, dotRadius, modifiedIndicatorPaint);
                canvas.DrawCircle(dotX, dotY, dotRadius, modifiedIndicatorBorder);
            }

            int inputCount = GetFunctoidInputCount(instance);
            for (int i = 0; i < inputCount; i++) {
                float portY = GetFunctoidInputSlotY(instance, i);
                canvas.DrawCircle(absX, portY, 3.5f, portPaint);
                canvas.DrawCircle(absX, portY, 3.5f, portBorder);
            }

            canvas.DrawCircle(absX + instance.Width, instance.Y + (instance.Height / 2f), 3.5f, portPaint);
            canvas.DrawCircle(absX + instance.Width, instance.Y + (instance.Height / 2f), 3.5f, portBorder);
        }
    }

    private void RenderFunctoidToolPalette(SKCanvas canvas) {
        using var font = new SKFont(SKTypeface.Default, 11f);
        using var bodyPaint = new SKPaint { Color = new SKColor(240, 243, 248), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var headerPaint = new SKPaint { Color = new SKColor(52, 73, 94), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var actionBarPaint = new SKPaint { Color = new SKColor(44, 62, 80), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var borderPaint = new SKPaint { Color = new SKColor(44, 62, 80), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        using var titleTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var actionTextPaint = new SKPaint { Color = new SKColor(200, 214, 229), IsAntialias = true };
        using var toggleTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };

        using var iconStrokePaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        using var iconFillPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };

        SKColor saveIconColor = isCanvasDirty ? new SKColor(231, 76, 60) : new SKColor(52, 152, 219);
        using var saveIconStrokePaint = new SKPaint { Color = saveIconColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        using var saveIconFillPaint = new SKPaint { Color = saveIconColor, Style = SKPaintStyle.Fill, IsAntialias = true };

        using var categoryBgPaint = new SKPaint { Color = new SKColor(215, 222, 233), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var categoryTextPaint = new SKPaint { Color = new SKColor(60, 70, 80), IsAntialias = true };
        using var itemBoxPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var itemBorderPaint = new SKPaint { Color = new SKColor(195, 205, 215), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
        using var itemTextPaint = new SKPaint { Color = new SKColor(40, 40, 40), IsAntialias = true };

        renderedItemHitboxes.Clear();

        float currentHeight = isPaletteExpanded ? expandedPaletteHeight : PaletteHeaderHeight;
        paletteBounds = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + currentHeight);

        if (isPaletteExpanded) {
            canvas.DrawRoundRect(paletteBounds, 6f, 6f, bodyPaint);
        }

        var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);
        canvas.DrawRoundRect(headerRect, 6f, 6f, headerPaint);

        float row1Height = 32f;
        var row2Rect = new SKRect(paletteBounds.Left, paletteBounds.Top + row1Height, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);
        canvas.DrawRect(row2Rect, actionBarPaint);

        canvas.DrawText("Functoid Toolbox", paletteBounds.Left + 12f, paletteBounds.Top + 21f, font, titleTextPaint);

        float currentButtonRightEdge = paletteBounds.Right - 12f;
        float row1CenterY = paletteBounds.Top + (row1Height / 2f);

        float toggleX = currentButtonRightEdge - 8f;
        string toggleGlyph = isPaletteExpanded ? "▲" : "▼";
        canvas.DrawText(toggleGlyph, toggleX, paletteBounds.Top + 22f, SKTextAlign.Center, font, toggleTextPaint);

        currentButtonRightEdge -= 28f;

        saveBtnRect = new SKRect(currentButtonRightEdge - 10f, row1CenterY - 10f, currentButtonRightEdge + 10f, row1CenterY + 10f);
        canvas.DrawRect(saveBtnRect, saveIconStrokePaint);
        canvas.DrawRect(new SKRect(currentButtonRightEdge - 5f, saveBtnRect.Top, currentButtonRightEdge + 5f, saveBtnRect.Top + 6f), saveIconFillPaint);
        canvas.DrawRect(new SKRect(currentButtonRightEdge - 4f, saveBtnRect.Bottom - 6f, currentButtonRightEdge + 4f, saveBtnRect.Bottom), saveIconStrokePaint);

        currentButtonRightEdge -= 32f;

        loadBtnRect = new SKRect(currentButtonRightEdge - 11f, row1CenterY - 8f, currentButtonRightEdge + 11f, row1CenterY + 8f);
        using (var folderPath = new SKPath()) {
            folderPath.MoveTo(loadBtnRect.Left, loadBtnRect.Bottom);
            folderPath.LineTo(loadBtnRect.Left, loadBtnRect.Top);
            folderPath.LineTo(loadBtnRect.Left + 7f, loadBtnRect.Top);
            folderPath.LineTo(loadBtnRect.Left + 11f, loadBtnRect.Top + 4f);
            folderPath.LineTo(loadBtnRect.Right, loadBtnRect.Top + 4f);
            folderPath.LineTo(loadBtnRect.Right, loadBtnRect.Bottom);
            folderPath.Close();
            canvas.DrawPath(folderPath, iconStrokePaint);
        }

        currentButtonRightEdge -= 32f;

        clearBtnRect = new SKRect(currentButtonRightEdge - 9f, row1CenterY - 9f, currentButtonRightEdge + 9f, row1CenterY + 9f);
        canvas.DrawRect(new SKRect(clearBtnRect.Left + 2f, clearBtnRect.Top + 4f, clearBtnRect.Right - 2f, clearBtnRect.Bottom), iconStrokePaint);
        canvas.DrawLine(clearBtnRect.Left, clearBtnRect.Top + 3f, clearBtnRect.Right, clearBtnRect.Top + 3f, iconStrokePaint);
        canvas.DrawRect(new SKRect(currentButtonRightEdge - 3f, clearBtnRect.Top, currentButtonRightEdge + 3f, clearBtnRect.Top + 3f), iconStrokePaint);

        if (isPaletteExpanded) {
            float row2CenterY = paletteBounds.Top + row1Height + (row1Height / 2f);
            float row2LeftX = paletteBounds.Left + 12f;

            executeXsltBtnRect = new SKRect(row2LeftX, row2CenterY - 9f, row2LeftX + 18f, row2CenterY + 9f);
            float gearCenterX = executeXsltBtnRect.MidX;

            canvas.DrawCircle(gearCenterX, row2CenterY, 4f, iconStrokePaint);
            for (int i = 0; i < 8; i++) {
                canvas.Save();
                canvas.RotateDegrees(i * 45f, gearCenterX, row2CenterY);
                canvas.DrawRect(new SKRect(gearCenterX - 1.5f, row2CenterY - 9f, gearCenterX + 1.5f, row2CenterY - 6f), iconFillPaint);
                canvas.Restore();
            }

            float xsltBtnWidth = 24f;
            float xsltBtnHeight = 20f;
            xsltFileEllipsisRect = new SKRect(paletteBounds.Right - xsltBtnWidth - 12f, row2CenterY - (xsltBtnHeight / 2f), paletteBounds.Right - 12f, row2CenterY + (xsltBtnHeight / 2f));

            float checkboxWidth = 14f;
            float checkboxHeight = 14f;
            showOutputCheckboxRect = new SKRect(xsltFileEllipsisRect.Left - 115f, row2CenterY - (checkboxHeight / 2f), xsltFileEllipsisRect.Left - 115f + checkboxWidth, row2CenterY - (checkboxHeight / 2f) + checkboxHeight);

            using var boxBgPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
            using var boxBorderPaint = new SKPaint { Color = new SKColor(160, 175, 195), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
            using var checkPaint = new SKPaint { Color = new SKColor(40, 50, 70), Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
            using var labelFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 11f);
            using var labelTextPaint = new SKPaint { Color = new SKColor(200, 214, 229), IsAntialias = true };

            canvas.DrawRoundRect(showOutputCheckboxRect, 2f, 2f, boxBgPaint);
            canvas.DrawRoundRect(showOutputCheckboxRect, 2f, 2f, boxBorderPaint);

            if (showOutputChecked) {
                canvas.DrawLine(showOutputCheckboxRect.Left + 3f, showOutputCheckboxRect.MidY, showOutputCheckboxRect.Left + 6f, showOutputCheckboxRect.Bottom - 3f, checkPaint);
                canvas.DrawLine(showOutputCheckboxRect.Left + 6f, showOutputCheckboxRect.Bottom - 3f, showOutputCheckboxRect.Right - 2f, showOutputCheckboxRect.Top + 3f, checkPaint);
            }

            canvas.DrawText("Show output", showOutputCheckboxRect.Right + 6f, row2CenterY + 4f, labelFont, labelTextPaint);

            using (var btnBg = new SKPaint { Color = new SKColor(210, 220, 235), Style = SKPaintStyle.Fill, IsAntialias = true })
            using (var btnBorder = new SKPaint { Color = new SKColor(160, 175, 195), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true })
            using (var btnFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 12f))
            using (var btnTextPaint = new SKPaint { Color = new SKColor(40, 50, 70), IsAntialias = true }) {
                canvas.DrawRoundRect(xsltFileEllipsisRect, 3f, 3f, btnBg);
                canvas.DrawRoundRect(xsltFileEllipsisRect, 3f, 3f, btnBorder);
                canvas.DrawText("...", xsltFileEllipsisRect.MidX, xsltFileEllipsisRect.MidY + 4f, SKTextAlign.Center, btnFont, btnTextPaint);
            }

            string xsltFileName = !string.IsNullOrWhiteSpace(MainForm.XsltPath) ? Path.GetFileName(MainForm.XsltPath) : "Transform.xslt";
            string executeText = $"Execute ({xsltFileName})";
            SKRect textClipRect = new SKRect(executeXsltBtnRect.Right + 8f, paletteBounds.Top + row1Height, showOutputCheckboxRect.Left - 8f, paletteBounds.Top + (row1Height * 2f));

            canvas.Save();
            canvas.ClipRect(textClipRect);
            canvas.DrawText(executeText, executeXsltBtnRect.Right + 8f, row2CenterY + 4f, font, actionTextPaint);
            canvas.Restore();
        }

        if (isPaletteExpanded) {
            float itemWidth = paletteBounds.Width - 16f;
            float viewableHeight = paletteBounds.Height - PaletteHeaderHeight - 12f;

            maxToolboxContentHeight = CalculateTotalToolboxContentHeight();
            bool needsScrollbar = maxToolboxContentHeight > viewableHeight;
            float usableItemWidth = needsScrollbar ? itemWidth - 12f : itemWidth;

            float leftPadding = paletteBounds.Left + 8f;
            float itemHeight = 24f;
            float categoryHeaderHeight = 22f;

            canvas.Save();
            SKRect contentClipArea = new SKRect(paletteBounds.Left + 2f, paletteBounds.Top + PaletteHeaderHeight + 2f, paletteBounds.Right - 2f, paletteBounds.Bottom - 4f);
            canvas.ClipRect(contentClipArea);

            float startYPosition = paletteBounds.Top + PaletteHeaderHeight + 8f;
            float virtualTrackingY = startYPosition + toolboxVirtualScrollY;

            foreach (var category in FunctoidCategories) {
                SKRect catHeaderRect = new(leftPadding, virtualTrackingY, leftPadding + usableItemWidth, virtualTrackingY + categoryHeaderHeight);
                category.LastRenderedHeaderBounds = catHeaderRect;

                if (catHeaderRect.Bottom > contentClipArea.Top && catHeaderRect.Top < contentClipArea.Bottom) {
                    canvas.DrawRoundRect(catHeaderRect, 3f, 3f, categoryBgPaint);
                    string arrowToken = category.IsExpanded ? "▼ " : "► ";
                    canvas.DrawText($"{arrowToken}{category.Name.ToUpper()}", catHeaderRect.Left + 8f, catHeaderRect.Top + 15f, font, categoryTextPaint);
                }

                virtualTrackingY += categoryHeaderHeight + 4f;

                if (category.IsExpanded) {
                    foreach (var functoid in AvailableFunctoids) {
                        if (functoid.CategoryId != category.Id) continue;

                        SKRect rowRect = new(leftPadding + 6f, virtualTrackingY, leftPadding + usableItemWidth - 6f, virtualTrackingY + itemHeight);
                        renderedItemHitboxes[functoid] = rowRect;

                        if (rowRect.Bottom > contentClipArea.Top && rowRect.Top < contentClipArea.Bottom) {
                            canvas.DrawRoundRect(rowRect, 3f, 3f, itemBoxPaint);
                            canvas.DrawRoundRect(rowRect, 3f, 3f, itemBorderPaint);

                            using var dotPaint = new SKPaint { Color = GetCategoryColor(category.Color), Style = SKPaintStyle.Fill, IsAntialias = true };
                            canvas.DrawCircle(rowRect.Left + 12f, rowRect.MidY, 4f, dotPaint);
                            canvas.DrawText(functoid.Name, rowRect.Left + 24f, rowRect.Top + 16f, font, itemTextPaint);
                        }

                        virtualTrackingY += itemHeight + 4f;
                    }
                }
                virtualTrackingY += 6f;
            }

            canvas.Restore();

            if (needsScrollbar) {
                float scrollbarWidth = 5f;
                float trackLeft = paletteBounds.Right - scrollbarWidth - 6f;
                float trackTop = paletteBounds.Top + PaletteHeaderHeight + 6f;
                float trackHeight = viewableHeight;

                float viewRatio = trackHeight / maxToolboxContentHeight;
                float thumbHeight = Math.Max(25f, trackHeight * viewRatio);

                float scrollMaxVirtual = maxToolboxContentHeight - trackHeight;
                float scrollPercent = scrollMaxVirtual <= 0 ? 0 : (-toolboxVirtualScrollY / scrollMaxVirtual);
                float thumbTop = trackTop + ((trackHeight - thumbHeight) * scrollPercent);

                using var trackPaint = new SKPaint { Color = new SKColor(220, 225, 235, 120), Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawRoundRect(new SKRect(trackLeft, trackTop, trackLeft + scrollbarWidth, trackTop + trackHeight), 2.5f, 2.5f, trackPaint);

                using var thumbPaint = new SKPaint { Color = new SKColor(150, 165, 180, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawRoundRect(new SKRect(trackLeft, thumbTop, trackLeft + scrollbarWidth, thumbTop + thumbHeight), 2.5f, 2.5f, thumbPaint);
            }
        }

        canvas.DrawRoundRect(paletteBounds, 6f, 6f, borderPaint);
    }

    private void RenderMapConnections(SKCanvas canvas) {
        if (Connections == null) return;

        using var wirePaint = new SKPaint {
            Color = new SKColor(46, 204, 113),
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        foreach (var conn in Connections) {
            SKPoint startPt = GetSourceEndpointLocation(conn.Source);
            SKPoint endPt = GetTargetEndpointLocation(conn.Target);

            if (startPt.IsEmpty || endPt.IsEmpty) continue;

            using var path = new SKPath();
            path.MoveTo(startPt.X, startPt.Y);

            float deltaX = Math.Abs(endPt.X - startPt.X);
            float controlOffset = Math.Max(30f, deltaX * 0.5f);

            path.CubicTo(
                startPt.X + controlOffset, startPt.Y,
                endPt.X - controlOffset, endPt.Y,
                endPt.X, endPt.Y
            );

            canvas.DrawPath(path, wirePaint);
        }
    }

    #endregion Paint and Render methods

    private static SKColor GetCategoryColor(string colorName) {
        if (string.IsNullOrEmpty(colorName)) return SKColors.LightGray;
        System.Drawing.Color systemColor = System.Drawing.Color.FromName(colorName);
        if (systemColor.IsKnownColor) {
            return new SKColor(systemColor.R, systemColor.G, systemColor.B, systemColor.A);
        }
        return SKColors.LightGray;
    }

    private SKColor GetFunctoidCategoryColor(FunctoidInstance instance) {
        if (instance?.Definition == null || FunctoidCategories == null) return new SKColor(220, 225, 230);
        var category = FunctoidCategories.FirstOrDefault(c => c.Id == instance.Definition.CategoryId);
        if (category == null || string.IsNullOrWhiteSpace(category.Color)) return new SKColor(220, 225, 230);
        System.Drawing.Color systemColor = System.Drawing.Color.FromName(category.Color);
        return systemColor.IsKnownColor ? new SKColor(systemColor.R, systemColor.G, systemColor.B, systemColor.A) : new SKColor(220, 225, 230);
    }

    private static SchemaNode? FindNodeWithFullPathAtPosition(SchemaNode node, float mouseY, string parentPath, ref string foundPath) {
        string currentPath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath}/{node.Name}";
        float halfHeight = (node.LastRenderedHeight > 0 ? node.LastRenderedHeight : 22f) / 2f;

        if (node.IsExpanded && node.Children != null) {
            foreach (var child in node.Children) {
                var found = FindNodeWithFullPathAtPosition(child, mouseY, currentPath, ref foundPath);
                if (found != null) return found;
            }
        }

        float tolerance = halfHeight + 2f;
        if (mouseY >= node.LastRenderedY - tolerance && mouseY <= node.LastRenderedY + tolerance) {
            foundPath = currentPath;
            return node;
        }
        return null;
    }

    private MappingConnection? FindConnectionAtPosition(float mouseX, float mouseY, float maxDistance = 12f) {
        if (Connections == null) return null;

        foreach (var conn in Connections) {
            if (conn == null || conn.Source == null || conn.Target == null) continue;

            SKPoint startPt = GetSourceEndpointLocation(conn.Source);
            SKPoint endPt = GetTargetEndpointLocation(conn.Target);

            if (startPt.IsEmpty || endPt.IsEmpty) continue;

            float startX = startPt.X, startY = startPt.Y;
            float endX = endPt.X, endY = endPt.Y;

            float controlOffset = Math.Max(30f, Math.Abs(endX - startX) * 0.5f);
            float cp1X = startX + controlOffset, cp1Y = startY;
            float cp2X = endX - controlOffset, cp2Y = endY;

            float linearLength = (float)Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));
            int samples = Math.Clamp((int)(linearLength / 8f), 20, 100);

            for (int i = 0; i <= samples; i++) {
                float t = i / (float)samples;
                float mt = 1f - t;
                float f0 = mt * mt * mt;
                float f1 = 3f * mt * mt * t;
                float f2 = 3f * mt * t * t;
                float f3 = t * t * t;

                float ptX = (f0 * startX) + (f1 * cp1X) + (f2 * cp2X) + (f3 * endX);
                float ptY = (f0 * startY) + (f1 * cp1Y) + (f2 * cp2Y) + (f3 * endY);

                float dx = mouseX - ptX;
                float dy = mouseY - ptY;
                if ((float)Math.Sqrt(dx * dx + dy * dy) <= maxDistance) {
                    return conn;
                }
            }
        }
        return null;
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        lastMousePos = new PointF(e.X, e.Y);
        this.Focus();
        float mainContentHeight = Height - logPanelHeight;
        float headerHeight = 32f;
        float centerLeft = leftTreeWidth;
        float centerRight = Width - rightTreeWidth;

        switch (e.Button) {
            case MouseButtons.Left:
                // Expand / Collapse Chevron Intercept: Destination Tree
                if (e.Y > headerHeight && e.Y < mainContentHeight && e.X > centerRight && DestinationRoot != null) {
                    float contentMouseY = e.Y - (headerHeight + 8f + destinationVirtualScrollY);
                    float traversalY = 0f;
                    if (TryToggleNodeExpansion(DestinationRoot, contentMouseY, e.X, centerRight, 0, ref traversalY)) {
                        Invalidate();
                        return;
                    }
                }

                // Destination Panel Scrollbar Intercept
                if (e.X > centerRight && e.Y >= headerHeight && e.Y < mainContentHeight) {
                    float destViewableHeight = mainContentHeight - headerHeight;
                    if (maxDestinationContentHeight > destViewableHeight) {
                        float scrollbarWidth = 5f;
                        float trackLeft = Width - scrollbarWidth - 4f;
                        float trackTop = headerHeight + 4f;
                        float trackHeight = destViewableHeight - 8f;
                        float viewRatio = trackHeight / maxDestinationContentHeight;
                        float thumbHeight = Math.Max(25f, trackHeight * viewRatio);
                        float scrollMaxVirtual = maxDestinationContentHeight - trackHeight;
                        float scrollPercent = scrollMaxVirtual <= 0 ? 0 : (-destinationVirtualScrollY / scrollMaxVirtual);
                        float thumbTop = trackTop + ((trackHeight - thumbHeight) * scrollPercent);
                        SKRect trackRect = new SKRect(trackLeft, trackTop, trackLeft + scrollbarWidth, trackTop + trackHeight);
                        SKRect thumbRect = new SKRect(trackLeft, thumbTop, trackLeft + scrollbarWidth, thumbTop + thumbHeight);

                        if (trackRect.Contains(e.X, e.Y)) {
                            if (thumbRect.Contains(e.X, e.Y)) {
                                isDraggingDestinationScrollbar = true;
                            } else {
                                float trackScrollableHeight = trackHeight - thumbHeight;
                                if (trackScrollableHeight > 0) {
                                    float clickOffsetPercent = Math.Clamp((e.Y - trackTop - (thumbHeight / 2f)) / trackScrollableHeight, 0f, 1f);
                                    destinationVirtualScrollY = -clickOffsetPercent * scrollMaxVirtual;
                                }
                                isDraggingDestinationScrollbar = true;
                            }
                            Capture = true;
                            Invalidate();
                            return;
                        }
                    }
                }

                // Destination Edit & View Header Buttons Intercept
                if (destinationFileEditRect.Contains(e.X, e.Y)) {
                    if (!string.IsNullOrEmpty(MainForm.DestinationPath) && File.Exists(MainForm.DestinationPath)) {
                        try {
                            string xmlContent = File.ReadAllText(MainForm.DestinationPath);
                            using var editor = new ScriptEditorDialog(xmlContent, "Destination XML Schema");
                            if (editor.ShowDialog() == DialogResult.OK) {
                                File.WriteAllText(MainForm.DestinationPath, editor.ScriptContent);
                                this.DestinationRoot = MainForm.LoadSchemaFromFile(MainForm.DestinationPath);
                                LogMessage($"Destination Schema updated: {MainForm.DestinationPath}");
                                Invalidate();
                            }
                        } catch (Exception ex) {
                            MessageBox.Show($"Error opening schema editor:\n{ex.Message}", "Editor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    return;
                }

                if (destinationFileViewRect.Contains(e.X, e.Y)) {
                    if (!string.IsNullOrEmpty(MainForm.DestinationPath) && File.Exists(MainForm.DestinationPath)) {
                        using var viewerForm = new SkiaMapper.Forms.OutputViewerForm(MainForm.DestinationPath, "Target schema");
                        viewerForm.Show(this);
                    }
                    return;
                }

                if (destinationFileEllipsisRect.Contains(e.X, e.Y)) {
                    using var ofd = new OpenFileDialog { Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*", Title = "Select Destination Target XML Schema" };
                    if (ofd.ShowDialog() == DialogResult.OK) {
                        MainForm.DestinationPath = ofd.FileName;
                        ActiveFunctoids.Clear();
                        Connections.Clear();
                        dragSource = null;
                        draggingCanvasInstance = null;
                        try {
                            this.DestinationRoot = MainForm.LoadSchemaFromFile(ofd.FileName);
                        } catch (Exception ex) {
                            MessageBox.Show($"Failed to load destination schema tree:\n{ex.Message}", "Schema Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            this.DestinationRoot = null;
                        }
                        isCanvasDirty = false;
                        Invalidate();
                    }
                    return;
                }

                // Tool Palette Overlay Intercept
                if (paletteBounds.Contains(e.X, e.Y)) {
                    ProcessLeftClickToolPalette(e);
                    return;
                }

                // Canvas Functoids Intercept
                if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {
                    if (ProcessLeftClickCanvasFunctoids(e, centerLeft)) return;
                }

                // UI Layout Splitters Intercept
                if (ProcessLeftClickLayoutSplitters(e, mainContentHeight, centerLeft, centerRight)) return;

                // Canvas Wire Selection Intercept
                ProcessLeftClickWireSelection(e);
                break;

            case MouseButtons.Right:
                if (paletteBounds.Contains(e.X, e.Y)) return;

                if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {
                    if (ProcessRightClickCanvasFunctoids(e, centerLeft)) return;
                }

                ProcessRightClickWireSelection(e);
                break;
        }
    }

    private void ProcessLeftClickToolPalette(MouseEventArgs e) {
        var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);

        if (headerRect.Contains(e.X, e.Y)) {
            if (xsltFileEllipsisRect.Contains(e.X, e.Y)) {
                using var ofd = new OpenFileDialog { Filter = "XSLT Files (*.xslt;*.xsl)|*.xslt;*.xsl|All Files (*.*)|*.*", Title = "Select XSLT Stylesheet File" };
                if (ofd.ShowDialog() == DialogResult.OK) {
                    MainForm.XsltPath = ofd.FileName;
                    RehydrateFromXslt(ofd.FileName);
                    Invalidate();
                }
                return;
            }

            switch (e.Location) {
                case var loc when showOutputCheckboxRect.Contains(loc.X, loc.Y):
                    showOutputChecked = !showOutputChecked;
                    Invalidate();
                    return;

                case var loc when executeXsltBtnRect.Contains(loc.X, loc.Y):
                    BtnExecuteTransform_Click(this, EventArgs.Empty);
                    return;

                case var loc when clearBtnRect.Contains(loc.X, loc.Y):
                    if (MessageBox.Show("Are you sure you want to completely clear the canvas?", "Clear Canvas", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
                        ActiveFunctoids.Clear();
                        Connections.Clear();
                        dragSource = null;
                        draggingCanvasInstance = null;
                        isCanvasDirty = false;
                        Invalidate();
                    }
                    return;

                case var loc when saveBtnRect.Contains(loc.X, loc.Y):
                    logMessages.Clear();
                    string buildXsltPath(string src, string dest) => Path.Combine(Path.GetDirectoryName(dest) ?? string.Empty, $"{Path.GetFileNameWithoutExtension(src)}_{Path.GetFileNameWithoutExtension(dest)}.xslt");
                    MainForm.XsltPath = buildXsltPath(MainForm.SourcePath, MainForm.DestinationPath);

                    using (var sfd = new SaveFileDialog { Filter = "Mapping Files (*.xslt)|*.xslt", DefaultExt = "xslt", AddExtension = true, Title = "Save Mapping Project & Schemas", FileName = MainForm.XsltPath }) {
                        if (sfd.ShowDialog() == DialogResult.OK) {
                            var state = new MappingProjectState {
                                SourceSchemaPath = MainForm.SourcePath,
                                DestinationSchemaPath = MainForm.DestinationPath,
                                ActiveFunctoids = this.ActiveFunctoids.ToList(),
                                Connections = this.Connections.ToList()
                            };

                            var serializer = new XmlSerializer(typeof(MappingProjectState));
                            using var writer = new StreamWriter(sfd.FileName);
                            serializer.Serialize(writer, state);

                            LogMessage($"Saved Mapping XSLT: {sfd.FileName}");
                            isCanvasDirty = false;
                            Invalidate();
                        }
                    }
                    return;

                case var loc when loadBtnRect.Contains(loc.X, loc.Y):
                    using (var ofd = new OpenFileDialog { Filter = "XML & XSLT Files (*.xml;*.xslt;*.xsl)|*.xml;*.xslt;*.xsl|All Files (*.*)|*.*", Title = "Load Mapping Schema" }) {
                        if (ofd.ShowDialog() == DialogResult.OK) {
                            LoadConfiguration(File.ReadAllText(ofd.FileName));
                            this.FindForm()?.Text = $"Working on {ofd.FileName}";
                            isCanvasDirty = false;
                            Invalidate();
                        }
                    }
                    return;

                case var loc when loc.X > paletteBounds.Right - 28f:
                    isPaletteExpanded = !isPaletteExpanded;
                    Invalidate();
                    return;

                default:
                    isDraggingPalette = true;
                    paletteDragOffset = new PointF(e.X - paletteBounds.Left, e.Y - paletteBounds.Top);
                    Capture = true;
                    return;
            }
        }

        if (isPaletteExpanded) {
            float viewableHeight = paletteBounds.Height - PaletteHeaderHeight - 12f;

            if (maxToolboxContentHeight > viewableHeight) {
                float scrollbarWidth = 5f;
                float trackLeft = paletteBounds.Right - scrollbarWidth - 6f;
                float trackTop = paletteBounds.Top + PaletteHeaderHeight + 6f;
                SKRect scrollHitRect = new SKRect(trackLeft - 4f, trackTop, paletteBounds.Right, trackTop + viewableHeight);
                if (scrollHitRect.Contains(e.X, e.Y)) {
                    isDraggingToolboxScrollbar = true;
                    Capture = true;
                    return;
                }
            }

            foreach (var category in FunctoidCategories) {
                if (category.LastRenderedHeaderBounds.Contains(e.X, e.Y)) {
                    category.IsExpanded = !category.IsExpanded;
                    maxToolboxContentHeight = CalculateTotalToolboxContentHeight();
                    float minScrollY = -(maxToolboxContentHeight - viewableHeight);
                    if (minScrollY > 0) minScrollY = 0;
                    toolboxVirtualScrollY = Math.Clamp(toolboxVirtualScrollY, minScrollY, 0f);
                    Invalidate();
                    return;
                }
            }

            foreach (var itemEntry in renderedItemHitboxes) {
                if (itemEntry.Value.Contains(e.X, e.Y)) {
                    draggingPaletteFunctoid = itemEntry.Key;
                    paletteItemDragOffset = new PointF(55f, 18f);
                    Capture = true;
                    Invalidate();
                    return;
                }
            }
        }
    }

    private bool ProcessLeftClickCanvasFunctoids(MouseEventArgs e, float centerLeft) {
        for (int i = ActiveFunctoids.Count - 1; i >= 0; i--) {
            var instance = ActiveFunctoids[i];
            float absoluteX = centerLeft + instance.X;
            SKRect itemRect = new(absoluteX, instance.Y, absoluteX + instance.Width, instance.Y + instance.Height);

            if (itemRect.Contains(e.X, e.Y)) {
                if (e.X > itemRect.MidX) {
                    dragSource = new ConnectionEndpoint {
                        Type = ConnectionEndpointType.Functoid,
                        FunctoidInstanceId = instance.Id
                    };
                    currentDragPoint = new PointF(itemRect.Right, itemRect.MidY);
                } else {
                    draggingCanvasInstance = instance;
                    canvasInstanceDragOffset = new PointF(e.X - absoluteX, e.Y - instance.Y);
                }
                Capture = true;
                Invalidate();
                return true;
            }
        }
        return false;
    }

    private bool ProcessLeftClickLayoutSplitters(MouseEventArgs e, float mainContentHeight, float centerLeft, float centerRight) {
        if (Math.Abs(e.Y - mainContentHeight) < SplitterWidth) {
            isResizingLog = true;
            Capture = true;
            return true;
        }
        if (Math.Abs(e.X - centerLeft) < SplitterWidth) {
            isResizingLeft = true;
            Capture = true;
            return true;
        }
        if (Math.Abs(e.X - centerRight) < SplitterWidth) {
            isResizingRight = true;
            Capture = true;
            return true;
        }
        return false;
    }

    private void ProcessLeftClickWireSelection(MouseEventArgs e) {
        var leftHitConnection = FindConnectionAtPosition(e.X, e.Y);
        if (leftHitConnection != selectedConnection) {
            selectedConnection = leftHitConnection;
            if (leftHitConnection != null) selectedCanvasInstance = null;
            Invalidate();
        }
    }

    private bool ProcessRightClickCanvasFunctoids(MouseEventArgs e, float centerLeft) {
        for (int i = ActiveFunctoids.Count - 1; i >= 0; i--) {
            var instance = ActiveFunctoids[i];
            float absoluteX = centerLeft + instance.X;
            SKRect itemRect = new(absoluteX, instance.Y, absoluteX + instance.Width, instance.Y + instance.Height);

            if (itemRect.Contains(e.X, e.Y)) {
                contextTargetInstance = instance;
                functoidContextMenu.Show(this, e.Location);
                return true;
            }
        }
        return false;
    }

    private void ProcessRightClickWireSelection(MouseEventArgs e) {
        var rightHitConnection = FindConnectionAtPosition(e.X, e.Y);
        if (rightHitConnection != null) {
            contextTargetConnection = rightHitConnection;
            selectedConnection = rightHitConnection;
            selectedCanvasInstance = null;
            Invalidate();
            connectionContextMenu?.Show(this, e.Location);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Delete) {
            if (selectedCanvasInstance != null) {
                string targetId = selectedCanvasInstance.Id.ToString();
                var connectionsToRemove = Connections.Where(c =>
                    (c.Source?.Type == ConnectionEndpointType.Functoid && c.Source.FunctoidInstanceId?.ToString() == targetId) ||
                    (c.Target?.Type == ConnectionEndpointType.Functoid && c.Target.FunctoidInstanceId?.ToString() == targetId)
                ).ToArray();

                foreach (var connection in connectionsToRemove) {
                    Connections.Remove(connection);
                }

                ActiveFunctoids.Remove(selectedCanvasInstance);
                if (contextTargetInstance == selectedCanvasInstance) contextTargetInstance = null;
                selectedCanvasInstance = null;
                Invalidate();
                e.Handled = true;
                return;
            }

            if (selectedConnection != null) {
                Connections.Remove(selectedConnection);
                if (contextTargetConnection == selectedConnection) contextTargetConnection = null;
                selectedConnection = null;
                Invalidate();
                e.Handled = true;
                return;
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        lastMousePos = new PointF(e.X, e.Y);
        float centerLeft = leftTreeWidth;
        float centerRight = Width - rightTreeWidth;
        float mainContentHeight = Height - logPanelHeight;

        if (draggingCanvasInstance != null) {
            float proposedCanvasX = (e.X - centerLeft) - canvasInstanceDragOffset.X;
            float proposedCanvasY = e.Y - canvasInstanceDragOffset.Y;

            float maxCanvasWidth = centerRight - centerLeft;
            if (proposedCanvasX < 0) proposedCanvasX = 0;
            if (proposedCanvasX + draggingCanvasInstance.Width > maxCanvasWidth) proposedCanvasX = maxCanvasWidth - draggingCanvasInstance.Width;

            if (proposedCanvasY < 0) proposedCanvasY = 0;
            if (proposedCanvasY + draggingCanvasInstance.Height > mainContentHeight) proposedCanvasY = mainContentHeight - draggingCanvasInstance.Height;

            draggingCanvasInstance.X = proposedCanvasX;
            draggingCanvasInstance.Y = proposedCanvasY;
            Invalidate();
            return;
        }

        if (draggingPaletteFunctoid != null || dragSource != null) {
            Invalidate();
            return;
        }

        if (isDraggingPalette) {
            Cursor = Cursors.SizeAll;
            float width = paletteBounds.Width;
            float height = paletteBounds.Height;
            float newLeft = e.X - paletteDragOffset.X;
            float newTop = e.Y - paletteDragOffset.Y;

            paletteBounds = new SKRect(newLeft, newTop, newLeft + width, newTop + height);
            Invalidate();
            return;
        }

        if (isResizingLog) {
            logPanelHeight = Height - e.Y;
            PerformLayout();
            Invalidate();
            return;
        } else if (isResizingLeft) {
            leftTreeWidth = Math.Max(150f, e.X);
            PerformLayout();
            Invalidate();
            return;
        } else if (isResizingRight) {
            rightTreeWidth = Math.Max(150f, Width - e.X);
            PerformLayout();
            Invalidate();
            return;
        }

        var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);
        if (headerRect.Contains(e.X, e.Y)) {
            if (clearBtnRect.Contains(e.X, e.Y) || loadBtnRect.Contains(e.X, e.Y) || saveBtnRect.Contains(e.X, e.Y) || e.X > paletteBounds.Right - 28f) {
                Cursor = Cursors.Default;
            } else {
                Cursor = Cursors.SizeAll;
            }
        } else if (Math.Abs(e.Y - mainContentHeight) < SplitterWidth) {
            Cursor = Cursors.HSplit;
        } else if (Math.Abs(e.X - leftTreeWidth) < SplitterWidth || Math.Abs(e.X - (Width - rightTreeWidth)) < SplitterWidth) {
            Cursor = Cursors.VSplit;
        } else {
            Cursor = Cursors.Default;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        float centerLeft = leftTreeWidth;
        float centerRight = Width - rightTreeWidth;
        float mainContentHeight = Height - logPanelHeight;

        if (draggingPaletteFunctoid != null) {
            if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {
                float localX = (e.X - centerLeft) - paletteItemDragOffset.X;
                float localY = e.Y - paletteItemDragOffset.Y;

                if (localX < 0) localX = 0;
                if (localX + 110f > (centerRight - centerLeft)) localX = (centerRight - centerLeft) - 110f;
                if (localY < 0) localY = 0;
                if (localY + 36f > mainContentHeight) localY = mainContentHeight - 36f;

                string defaultMethodName = ExtractMethodNameFromTemplate(
                    draggingPaletteFunctoid.ScriptTemplate,
                    draggingPaletteFunctoid.Name,
                    draggingPaletteFunctoid.Id
                );

                if (draggingPaletteFunctoid.InputParametersCount <= 1 && !string.IsNullOrWhiteSpace(draggingPaletteFunctoid.ScriptTemplate)) {
                    var constraints = FunctoidAnalyzer.AnalyzeTemplate(draggingPaletteFunctoid.ScriptTemplate);
                    draggingPaletteFunctoid.InputParametersCount = constraints.InitialSlots;
                }

                var newInstance = new FunctoidInstance {
                    Id = Guid.NewGuid(),
                    X = localX,
                    Y = localY,
                    Width = 110f,
                    Height = 36f,
                    Definition = draggingPaletteFunctoid,
                    CustomScriptBody = draggingPaletteFunctoid.ScriptTemplate?.Trim(),
                    CustomMethodName = defaultMethodName
                };

                ActiveFunctoids.Add(newInstance);
            }

            draggingPaletteFunctoid = null;
            Capture = false;
            Invalidate();
            return;
        }

        if (dragSource != null) {
            ConnectionEndpoint? dragTarget = null;

            // Check if dropped onto the Destination Panel control area (inclusive of exact right edge boundary)
            if (e.X >= centerRight && e.X <= Width && e.Y < mainContentHeight && DestinationRoot != null) {
                Point clientPoint = DestinationPanelControl.PointToClient(this.PointToScreen(new Point((int)e.X, (int)e.Y)));
                string? matchedPath = DestinationPanelControl?.HitTestNodePath(clientPoint.Y);

                if (!string.IsNullOrEmpty(matchedPath)) {
                    dragTarget = new ConnectionEndpoint {
                        Type = ConnectionEndpointType.DestinationNode,
                        NodePath = matchedPath,
                        InputIndex = 0
                    };
                }
            }
            // Check if dropped onto an active Functoid input port on the middle canvas
            else if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {
                foreach (var instance in ActiveFunctoids) {
                    float absoluteX = centerLeft + instance.X;
                    SKRect itemRect = new(absoluteX, instance.Y, absoluteX + instance.Width, instance.Y + instance.Height);

                    if (itemRect.Contains(e.X, e.Y) && e.X <= itemRect.MidX) {
                        int maxParams = GetFunctoidInputCount(instance);
                        int safeMaxParams = maxParams > 0 ? maxParams : 1;
                        float relativeY = e.Y - instance.Y;
                        float sectorHeight = instance.Height / safeMaxParams;
                        int calculatedSlotIndex = (int)(relativeY / sectorHeight);

                        if (calculatedSlotIndex < 0) calculatedSlotIndex = 0;
                        if (calculatedSlotIndex >= safeMaxParams) calculatedSlotIndex = safeMaxParams - 1;

                        dragTarget = new ConnectionEndpoint {
                            Type = ConnectionEndpointType.Functoid,
                            FunctoidInstanceId = instance.Id,
                            NodePath = string.Empty,
                            InputIndex = calculatedSlotIndex
                        };
                        break;
                    }
                }
            }

            if (dragTarget != null) {
                bool isSelfLoop = dragSource.Type == ConnectionEndpointType.Functoid &&
                                dragTarget.Type == ConnectionEndpointType.Functoid &&
                                dragSource.FunctoidInstanceId == dragTarget.FunctoidInstanceId;

                if (!isSelfLoop) {
                    if (this.Connections != null) {
                        var duplicateWire = this.Connections.FirstOrDefault(c =>
                            c.Target != null &&
                            c.Target.Type == dragTarget.Type &&
                            (dragTarget.Type == ConnectionEndpointType.Functoid
                                ? (c.Target.FunctoidInstanceId == dragTarget.FunctoidInstanceId && c.Target.InputIndex == dragTarget.InputIndex)
                                : (c.Target.NodePath == dragTarget.NodePath))
                        );

                        if (duplicateWire != null) {
                            this.Connections.Remove(duplicateWire);
                        }
                    }

                    Connections?.Add(new MappingConnection {
                        Source = dragSource,
                        Target = dragTarget
                    });
                }
            }

            dragSource = null;
            Capture = false;
            Invalidate();
        }

        draggingCanvasInstance = null;
        isDraggingPalette = false;
        isResizingLog = false;
        isResizingLeft = false;
        isResizingRight = false;
        isDraggingDestinationScrollbar = false;
    }

    private SKPoint GetSourceEndpointLocation(ConnectionEndpoint source) {
        if (source == null) return SKPoint.Empty;

        if (source.Type == ConnectionEndpointType.SourceNode) {
            // Query the segregated control for the exact tracked workspace Y-coordinate
            float? workspaceY = SourcePanelControl?.GetNodeWorkspaceY(source.NodePath);
            if (workspaceY.HasValue) {
                return new SKPoint(leftTreeWidth, workspaceY.Value);
            }

            // Fallback if the node is collapsed or not currently rendered in view
            return new SKPoint(leftTreeWidth, 40f);
        }

        if (source.Type == ConnectionEndpointType.Functoid && source.FunctoidInstanceId.HasValue) {
            var instance = ActiveFunctoids.FirstOrDefault(f => f.Id == source.FunctoidInstanceId.Value);
            if (instance != null) {
                float absoluteX = leftTreeWidth + instance.X + instance.Width;
                float absoluteY = instance.Y + (instance.Height / 2f);
                return new SKPoint(absoluteX, absoluteY);
            }
        }

        return SKPoint.Empty;
    }


    private SKPoint GetTargetEndpointLocation(ConnectionEndpoint target) {
        if (target == null) return SKPoint.Empty;

        if (target.Type == ConnectionEndpointType.DestinationNode) {
            float? workspaceY = DestinationPanelControl?.GetNodeWorkspaceY(target.NodePath);
            if (workspaceY.HasValue) {
                float absoluteX = Width - rightTreeWidth;
                return new SKPoint(absoluteX, workspaceY.Value);
            }
            return SKPoint.Empty;
        }

        if (target.Type == ConnectionEndpointType.Functoid && target.FunctoidInstanceId.HasValue) {
            var instance = ActiveFunctoids.FirstOrDefault(f => f.Id == target.FunctoidInstanceId.Value);
            if (instance != null) {
                float absoluteX = leftTreeWidth + instance.X;
                int availableSlots = GetFunctoidInputCount(instance);
                int safeInputIndex = Math.Clamp(target.InputIndex, 0, Math.Max(0, availableSlots - 1));
                float absoluteY = GetFunctoidInputSlotY(instance, safeInputIndex);
                return new SKPoint(absoluteX, absoluteY);
            }
        }
        return SKPoint.Empty;
    }
    private static string ExtractMethodNameFromTemplate(string template, string functoidName, int functoidId) {
        if (string.IsNullOrWhiteSpace(template)) {
            return $"Transform_{functoidName.Replace(" ", "")}_{functoidId}";
        }
        try {
            int openParenIndex = template.IndexOf('(');
            if (openParenIndex > 0) {
                string leadingToParen = template.Substring(0, openParenIndex).Trim();
                int lastSpaceIndex = leadingToParen.LastIndexOf(' ');
                if (lastSpaceIndex >= 0) {
                    string parsedName = leadingToParen.Substring(lastSpaceIndex + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(parsedName)) {
                        return parsedName;
                    }
                }
            }
        } catch { }
        return $"Transform_{functoidName.Replace(" ", "")}_{functoidId}";
    }

    public void LoadConfiguration(string xmlContent) {
        try {
            using var reader = new StringReader(xmlContent);
            var serializer = new XmlSerializer(typeof(MappingProjectState));
            if (serializer.Deserialize(reader) is MappingProjectState state) {

                if (!string.IsNullOrWhiteSpace(state.SourceSchemaPath) && File.Exists(state.SourceSchemaPath)) {
                    MainForm.SourcePath = state.SourceSchemaPath;
                    SourcePanelControl.SourcePath = state.SourceSchemaPath;
                    SourcePanelControl.SourceRoot = MainForm.LoadSchemaFromFile(state.SourceSchemaPath);
                }

                if (!string.IsNullOrWhiteSpace(state.DestinationSchemaPath) && File.Exists(state.DestinationSchemaPath)) {
                    MainForm.DestinationPath = state.DestinationSchemaPath;
                    DestinationRoot = MainForm.LoadSchemaFromFile(state.DestinationSchemaPath);
                }

                ActiveFunctoids.Clear();
                if (state.ActiveFunctoids != null) {
                    foreach (var f in state.ActiveFunctoids) ActiveFunctoids.Add(f);
                }

                Connections.Clear();
                if (state.Connections != null) {
                    foreach (var c in state.Connections) Connections.Add(c);
                }

                isCanvasDirty = false;
                Invalidate();
            }
        } catch (Exception ex) {
            MessageBox.Show($"Failed to load mapping project:\n{ex.Message}", "Deserialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

    private void RehydrateFromXslt(string xsltPath) {
        try {
            if (!File.Exists(xsltPath)) return;
            string content = File.ReadAllText(xsltPath);

            var sourceMatch = System.Text.RegularExpressions.Regex.Match(content, @"<!--\s*SourcePath:\s*(.*?)\s*-->");
            if (sourceMatch.Success) {
                string srcPath = sourceMatch.Groups[1].Value.Trim();
                if (File.Exists(srcPath)) {
                    MainForm.SourcePath = srcPath;
                    SourcePanelControl.SourcePath = srcPath;
                    SourcePanelControl.SourceRoot = MainForm.LoadSchemaFromFile(srcPath);
                }
            }

            var destMatch = System.Text.RegularExpressions.Regex.Match(content, @"<!--\s*DestinationPath:\s*(.*?)\s*-->");
            if (destMatch.Success) {
                string destPath = destMatch.Groups[1].Value.Trim();
                if (File.Exists(destPath)) {
                    MainForm.DestinationPath = destPath;
                    DestinationRoot = MainForm.LoadSchemaFromFile(destPath);
                }
            }

            this.Invalidate();
        } catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine($"[REHYDRATE ERROR] {ex.Message}");
        }
    }
    #region Script Modification & Connection Pruning Helpers

    /// <summary>
    /// Called when a functoid's C# script block is edited inside ScriptEditorDialog.
    /// Triggers Roslyn re-analysis of parameter ports and updates custom method name tracking.
    /// </summary>
    public void OnFunctoidScriptModified(FunctoidInstance instance) {
        if (instance == null) return;

        // 1. Force Roslyn to re-parse the altered method signature code block
        string? updatedCode = instance.CustomScriptBody;
        int parsedSlotsCount = 0;

        if (!string.IsNullOrWhiteSpace(updatedCode)) {
            var constraints = FunctoidAnalyzer.AnalyzeTemplate(updatedCode);
            parsedSlotsCount = constraints.InitialSlots;

            if (instance.Definition != null) {
                instance.Definition.InputParametersCount = parsedSlotsCount;
            }

            // Derive and update the method name from the script body
            string extractedName = ExtractMethodNameFromTemplate(updatedCode, instance.Definition?.Name ?? "Functoid", 0);
            if (!string.IsNullOrWhiteSpace(extractedName)) {
                instance.CustomMethodName = extractedName;
            }
        }

        // 2. Prune obsolete connections targeting ports that no longer exist
        PruneOrphanedConnections(instance);

        // 3. Force canvas redraw to reflect slot layout changes
        Invalidate();
    }

    /// <summary>
    /// Removes wire connections targeting input ports that exceed the updated port count limit.
    /// </summary>
    public void PruneOrphanedConnections(FunctoidInstance instance) {
        if (instance == null || instance.Definition == null) return;

        int newMaxSlots = instance.Definition.InputParametersCount;

        if (Connections != null) {
            var obsoleteConnections = Connections.Where(c =>
                c.Target != null &&
                c.Target.Type == ConnectionEndpointType.Functoid &&
                c.Target.FunctoidInstanceId == instance.Id &&
                c.Target.InputIndex >= newMaxSlots
            ).ToList();

            foreach (var connection in obsoleteConnections) {
                Connections.Remove(connection);
            }
        }
    }

    #endregion

}