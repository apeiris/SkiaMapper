using SkiaMapper.Models;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
namespace SkiaMapper.Controls;

public class SkiaMapperControl : SKControl {
    // Panel Dimension Metrics (Splitters)
    private float leftTreeWidth = 280f;
    private float rightTreeWidth = 280f;
    private float logPanelHeight = 120f;
    private const float SplitterWidth = 5f;
    // Tool Palette Window Metrics
    private SKRect paletteBounds = new SKRect(310f, 30f, 550f, 450f);
    private const float PaletteHeaderHeight = 64f;
    private bool isDraggingPalette = false;
    private PointF paletteDragOffset;
    private bool isPaletteExpanded = true;
    // Remembers its size when open
    private float expandedPaletteHeight = 420f;
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
    // Functoid tool box header button hit boxes (Save, Load, Clear, Execute XSLT)
    private SKRect saveBtnRect;
    private SKRect loadBtnRect;
    private SKRect clearBtnRect;
    private SKRect executeXsltBtnRect;
    private const float HeaderIconSize = 20f;
    private ContextMenuStrip functoidContextMenu;
    private ContextMenuStrip connectionContextMenu;
    private FunctoidInstance? contextTargetInstance = null;
    private FunctoidInstance? selectedCanvasInstance = null; // <-- ADD THIS LINE
    private MappingConnection? selectedConnection = null; // <-- ADD THIS LINE
    private MappingConnection? contextTargetConnection = null;
    private float toolboxVirtualScrollY = 0f;
    private float maxToolboxContentHeight = 0f;
    private bool isDraggingToolboxScrollbar = false;
    private float toolboxScrollbarDragStartY = 0f;
    private float toolboxScrollbarDragStartScrollY = 0f;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SchemaNode? SourceRoot { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SchemaNode? DestinationRoot { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<FunctoidCategory> FunctoidCategories { get; set; } = new();
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<FunctoidDefinition> AvailableFunctoids { get; set; } = new();
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ObservableCollection<FunctoidInstance> ActiveFunctoids { get; } = new();
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ObservableCollection<MappingConnection> Connections { get; } = new();
    private ConnectionEndpoint? dragSource = null;
    private PointF currentDragPoint;
    public SkiaMapperControl() {
        this.DoubleBuffered = true;
        this.Dock = DockStyle.Fill;
        this.SetStyle(ControlStyles.Selectable, true);

        // 1. Initialize Functoid Context Menu
        functoidContextMenu = new ContextMenuStrip();

        var editScriptItem = new ToolStripMenuItem("Configure Script Block...");
        editScriptItem.Click += EditScriptItem_Click;
        functoidContextMenu.Items.Add(editScriptItem);

        // --- ADDITION: Add Delete Item to Functoid Context Menu ---
        var deleteFunctoidItem = new ToolStripMenuItem("Delete Functoid");
        deleteFunctoidItem.Click += DeleteFunctoidItem_Click;
        functoidContextMenu.Items.Add(deleteFunctoidItem);

        // 2. Initialize Connection Wire Context Menu
        connectionContextMenu = new ContextMenuStrip();
        var deleteConnItem = new ToolStripMenuItem("Delete Connection Wire");
        deleteConnItem.Click += DeleteConnectionItem_Click;
        connectionContextMenu.Items.Add(deleteConnItem);

        // 3. Native Mouse Wheel Scroll Intercept Hook
        this.MouseWheel += SkiaMapperControl_MouseWheel;

        // 4. Automated Canvas State Architecture Hook
        // Automatically sets IsCanvasDirty = true on any layout additions, deletions, or clears
        ActiveFunctoids.CollectionChanged += OnCanvasStructureChanged;
        Connections.CollectionChanged += OnCanvasStructureChanged;
    }
    /// <summary>
    /// Execution pipeline endpoint triggered by the toolbox transform button.
    /// </summary>
    private void BtnExecuteTransform_Click(object? sender, EventArgs e) {
        // Safety Check: Avoid compilation if canvas is empty
        if (this.ActiveFunctoids == null || this.ActiveFunctoids.Count == 0) {
            MessageBox.Show(
                "The canvas is empty. Add functoids and connection wires before compiling XSLT.",
                "XSLT Engine Notice",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        try {
            // --- DUMMY STUB PIPELINE ---
            // Future home of your Roslyn script engine parameter updates and template compilation.
            string totalFunctoids = this.ActiveFunctoids.Count.ToString();
            string totalConnections = this.Connections?.Count.ToString() ?? "0";

            MessageBox.Show(
                $"[XSLT Engine Stub]\n\n" +
                $"Successfully read active map state:\n" +
                $"• Functoid Blocks: {totalFunctoids}\n" +
                $"• Active Wires: {totalConnections}\n\n" +
                $"Ready to execute dynamic transformation compilation pass.",
                "Pipeline Verification",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        } catch (Exception ex) {
            MessageBox.Show(
                $"Transformation processing pipeline failed:\n{ex.Message}",
                "Compilation Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
    private void OnCanvasStructureChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
        isCanvasDirty = true;
    }
    private void SkiaMapperControl_MouseWheel(object? sender, MouseEventArgs e) {
        if (isPaletteExpanded && paletteBounds.Contains(e.X, e.Y)) {
            float viewableHeight = paletteBounds.Height - PaletteHeaderHeight - 12f;
            maxToolboxContentHeight = CalculateTotalToolboxContentHeight();

            if (maxToolboxContentHeight <= viewableHeight) {
                toolboxVirtualScrollY = 0f;
                Invalidate();
                return;
            }

            // Adjust scroll tracking offset (inverted standard wheel direction delta)
            int scrollDelta = e.Delta > 0 ? 30 : -30;
            toolboxVirtualScrollY += scrollDelta;

            // Secure bounds clamp
            float minScrollY = -(maxToolboxContentHeight - viewableHeight);
            toolboxVirtualScrollY = Math.Clamp(toolboxVirtualScrollY, minScrollY, 0f);

            Invalidate(); // Force repaint
        }
    }
    private float CalculateTotalToolboxContentHeight() {
        float itemHeight = 24f;
        float categoryHeaderHeight = 22f;
        float totalHeight = 0f;

        foreach (var category in FunctoidCategories) {
            totalHeight += categoryHeaderHeight + 4f; // Header + padding

            if (category.IsExpanded) {
                foreach (var functoid in AvailableFunctoids) {
                    if (functoid.CategoryId != category.Id) continue;
                    totalHeight += itemHeight + 4f; // Item row + padding
                }
            }
            totalHeight += 6f; // Gap between sections
        }
        return totalHeight;
    }

    private void DeleteFunctoidItem_Click(object? sender, EventArgs e) {
        if (contextTargetInstance != null) {
            // 1. Cascade delete all wires tied to this specific instance ID (String-to-String comparison)
            string targetId = contextTargetInstance.Id.ToString();
            // --- REVISED: Safe target tracking lookup for ObservableCollection ---
            var connectionsToRemove = Connections.Where(c =>
                (c.Source?.Type == ConnectionEndpointType.Functoid && c.Source.FunctoidInstanceId?.ToString() == targetId) ||
                (c.Target?.Type == ConnectionEndpointType.Functoid && c.Target.FunctoidInstanceId?.ToString() == targetId)
            ).ToArray();

            foreach (var connection in connectionsToRemove) {
                Connections.Remove(connection);
            }
            // 2. Remove the block from the active tracking layout
            ActiveFunctoids.Remove(contextTargetInstance);

            // 3. Clear selection states cleanly
            if (selectedCanvasInstance == contextTargetInstance) {
                selectedCanvasInstance = null;
            }
            contextTargetInstance = null;

            // 4. Force immediate repaint
            Invalidate();
        }
    }
    private void DeleteConnectionItem_Click(object? sender, EventArgs e) {
        if (contextTargetConnection != null) {
            // This single line now removes the wire, automatically sets IsCanvasDirty = true,
            // and instantly schedules a new OnPaint layout pass.
            Connections.Remove(contextTargetConnection);

            // Clear active selection tracking if it matches the deleted wire
            if (selectedConnection == contextTargetConnection) {
                selectedConnection = null;
            }

            contextTargetConnection = null;
            this.Invalidate();
        }
    }
    private MappingConnection? FindConnectionAtPosition(float mouseX, float mouseY, float maxDistance = 12f) {
        if (Connections == null) return null;

        float centerLeft = leftTreeWidth;

        foreach (var conn in Connections) {
            if (conn == null || conn.Source == null || conn.Target == null) continue;

            float startX = 0f, startY = 0f;
            float endX = 0f, endY = 0f;

            // 1. Resolve Start Point
            if (conn.Source.Type == ConnectionEndpointType.Functoid) {
                if (conn.Source.FunctoidInstanceId == null || ActiveFunctoids == null) continue;

                // --- REVISED: Use LINQ FirstOrDefault instead of List.Find ---
                var instance = ActiveFunctoids.FirstOrDefault(f => f != null && f.Id == conn.Source.FunctoidInstanceId);
                if (instance != null) {
                    startX = centerLeft + instance.X + instance.Width;
                    startY = instance.Y + (instance.Height / 2f);
                } else continue;
            } else {
                if (string.IsNullOrEmpty(conn.Source.NodePath)) continue;
                startY = FindNodeY(SourceRoot, conn.Source.NodePath);
                startX = leftTreeWidth - 25f;
                if (startY == -1) continue;
            }

            // 2. Resolve End Point
            if (conn.Target.Type == ConnectionEndpointType.Functoid) {
                if (conn.Target.FunctoidInstanceId == null || ActiveFunctoids == null) continue;
                // --- REVISED: Use LINQ FirstOrDefault instead of List.Find ---
                var instance = ActiveFunctoids.FirstOrDefault(f => f != null && f.Id == conn.Target.FunctoidInstanceId);
                if (instance != null) {
                    endX = centerLeft + instance.X;
                    endY = instance.Y + (instance.Height / 2f);
                } else continue;
            } else {
                if (string.IsNullOrEmpty(conn.Target.NodePath)) continue;
                endY = FindNodeY(DestinationRoot, conn.Target.NodePath);
                endX = Width - rightTreeWidth + 5f;
                if (endY == -1) continue;
            }

            // 3. Setup Cubic Bezier Control Points
            float controlOffset = Math.Max(30f, Math.Abs(endX - startX) * 0.5f);
            float cp1X = startX + controlOffset;
            float cp1Y = startY;
            float cp2X = endX - controlOffset;
            float cp2Y = endY;

            // 4. DYNAMIC SAMPLING ENGINE
            // Estimate the linear bounding distance to scale step density accurately
            float linearLength = (float)Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));

            // Dynamically assign samples: 1 sample per every 8 pixels of span, clamped between 20 and 100
            int samples = Math.Clamp((int)(linearLength / 8f), 20, 100);

            for (int i = 0; i <= samples; i++) {
                float t = i / (float)samples;

                // Polynomial Curve Weighting
                float mt = 1f - t;
                float f0 = mt * mt * mt;
                float f1 = 3f * mt * mt * t;
                float f2 = 3f * mt * t * t;
                float f3 = t * t * t;

                float ptX = (f0 * startX) + (f1 * cp1X) + (f2 * cp2X) + (f3 * endX);
                float ptY = (f0 * startY) + (f1 * cp1Y) + (f2 * cp2Y) + (f3 * endY);

                // Distance Check
                float dx = mouseX - ptX;
                float dy = mouseY - ptY;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (distance <= maxDistance) {
                    return conn; // Success
                }
            }
        }
        return null;
    }
    private SchemaNode? FindNodeByPath(SchemaNode? currentNode, string? path) {
        if (currentNode == null || string.IsNullOrEmpty(path)) return null;
        if (currentNode.Name == path) return currentNode;

        if (currentNode.Children != null) {
            foreach (var child in currentNode.Children) {
                var found = FindNodeByPath(child, path);
                if (found != null) return found;
            }
        }
        return null;
    }
    private void EditScriptItem_Click(object? sender, EventArgs e) {
        if (contextTargetInstance != null) {
            using var editor = new Forms.ScriptEditorDialog(contextTargetInstance);
            if (editor.ShowDialog() == DialogResult.OK) {
                // Script property allocations saved cleanly inside modal context
                isCanvasDirty = true;
                Invalidate();
            }
        }

    }
    #region Paint and Render methods
    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
        base.OnPaintSurface(e);
        var canvas = e.Surface.Canvas;
        int w = e.Info.Width;
        int h = e.Info.Height;
        canvas.Clear(SKColors.White);
        float centerLeft = leftTreeWidth;  // Declared ONCE cleanly for the entire method scope
        float centerRight = w - rightTreeWidth;
        float mainContentHeight = h - logPanelHeight;
        using var bgPaint = new SKPaint(); 
        // 1. Draw Viewport Background Panels
        bgPaint.Color = SKColors.GhostWhite;
        canvas.DrawRect(0, 0, leftTreeWidth, mainContentHeight, bgPaint);
        canvas.DrawRect(centerRight, 0, rightTreeWidth, mainContentHeight, bgPaint);
        bgPaint.Color = new SKColor(245, 247, 250);
        canvas.DrawRect(centerLeft, 0, centerRight - centerLeft, mainContentHeight, bgPaint);
        bgPaint.Color = new SKColor(30, 30, 30);
        canvas.DrawRect(0, mainContentHeight, w, logPanelHeight, bgPaint);
        float currentY = 10f;   
        // 2. Draw Trees
        if (SourceRoot != null) RenderTreeElement(canvas, SourceRoot, 15f, ref currentY, true);
        currentY = 10f;
        if (DestinationRoot != null) RenderTreeElement(canvas, DestinationRoot, centerRight + 15f, ref currentY, false);
        RenderActiveGridFunctoids(canvas, centerLeft); 
        // 3. Draw Placed Canvas Grid Functoids
        RenderFunctoidToolPalette(canvas);  
        // 4. Draw Floating Tool Palette Window
        bgPaint.Color = SKColors.DarkGray; 
        // 5. Draw Layout Grid Splitters
        canvas.DrawRect(leftTreeWidth - 2, 0, SplitterWidth, mainContentHeight, bgPaint);
        canvas.DrawRect(centerRight - 3, 0, SplitterWidth, mainContentHeight, bgPaint);
        canvas.DrawRect(0, mainContentHeight - 2, w, SplitterWidth, bgPaint);
        RenderMapConnections(canvas);
        // 6. Draw Permanent Map Wire Connections
        if (dragSource != null) { 
            // 7. Draw Live Dragging Connection Line Link
            using var dragPaint = new SKPaint { Color = SKColors.Orange, StrokeWidth = 2f, IsAntialias = true, Style = SKPaintStyle.Stroke };
            using var previewPath = new SKPath();
            previewPath.MoveTo(currentDragPoint.X, currentDragPoint.Y);
            // FIXED: Using lastMousePos (cached from MouseMove) instead of e.X/e.Y
            float controlOffset = Math.Max(30f, Math.Abs(lastMousePos.X - currentDragPoint.X) * 0.5f);
            float cp1X = currentDragPoint.X + controlOffset;
            float cp1Y = currentDragPoint.Y;
            float cp2X = lastMousePos.X - controlOffset;
            float cp2Y = lastMousePos.Y;
            previewPath.CubicTo(cp1X, cp1Y, cp2X, cp2Y, lastMousePos.X, lastMousePos.Y);
            canvas.DrawPath(previewPath, dragPaint);
        }
        if (draggingPaletteFunctoid != null) {  
            // 8. Draw Ghost Palette Item Preview Box
            using var ghostBoxPaint = new SKPaint { Color = new SKColor(52, 152, 219, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var ghostBorderPaint = new SKPaint { Color = new SKColor(41, 128, 185), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            using var ghostTextPaint = new SKPaint { Color = SKColors.White, TextSize = 11f, IsAntialias = true, TextAlign = SKTextAlign.Center };
            float ghostX = lastMousePos.X - paletteItemDragOffset.X; // FIXED: Using lastMousePos instead of e.X/e.Y
            float ghostY = lastMousePos.Y - paletteItemDragOffset.Y;
            SKRect ghostRect = new SKRect(ghostX, ghostY, ghostX + 110f, ghostY + 36f);
            canvas.DrawRoundRect(ghostRect, 4f, 4f, ghostBoxPaint);
            canvas.DrawRoundRect(ghostRect, 4f, 4f, ghostBorderPaint);
            canvas.DrawText(draggingPaletteFunctoid.Name, ghostRect.MidX, ghostRect.MidY + 4f, ghostTextPaint);
        }
    }
    private void RenderTreeElement(SKCanvas canvas, SchemaNode node, float x, ref float currentY, bool isSourceTree) {
        using var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 12f, IsAntialias = true };
        using var nodeBoxPaint = new SKPaint { Color = new SKColor(220, 230, 242), Style = SKPaintStyle.Fill };
        using var nodeBorderPaint = new SKPaint { Color = new SKColor(120, 150, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

        float nodeHeight = 22f;
        float indentSpacing = 15f;

        node.LastRenderedY = currentY + (nodeHeight / 2f);
        node.LastRenderedHeight = nodeHeight;

        SKRect rowRect = new SKRect(x, currentY, x + (isSourceTree ? leftTreeWidth : rightTreeWidth) - 30f, currentY + nodeHeight);
        canvas.DrawRoundRect(rowRect, 2f, 2f, nodeBoxPaint);
        canvas.DrawRoundRect(rowRect, 2f, 2f, nodeBorderPaint);

        string expandGlyph = node.Children.Count > 0 ? (node.IsExpanded ? "▼ " : "► ") : "  ";
        canvas.DrawText($"{expandGlyph}{node.Name}", x + 6f, currentY + 16f, textPaint);

        currentY += nodeHeight + 4f;

        if (node.IsExpanded) {
            foreach (var child in node.Children) {
                RenderTreeElement(canvas, child, x + indentSpacing, ref currentY, isSourceTree);
            }
        }
    }
    private void RenderFunctoidParameterConnections(SKCanvas canvas, float centerLeft) {
        using var wirePaint = new SKPaint {
            Color = new SKColor(52, 152, 219, 220), // Clean slate blue connection wire
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.75f,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash(new float[] { 4f, 4f }, 0f) // Subtle dash to indicate execution routing pass
        };

        using var jointPaint = new SKPaint {
            Color = new SKColor(41, 128, 185),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Loop through every live canvas element to look for assigned inbound parameters
        foreach (var targetInstance in ActiveFunctoids) {
            if (targetInstance.ConnectedParameters == null) continue;

            //int maxParams = targetInstance.Definition?.InputParameters?.Count ?? 1;
            int maxParams = targetInstance.Definition?.InputParametersCount ?? 1; // <-- REVISED: Use direct count property for efficiency


            float sectorHeight = targetInstance.Height / maxParams;
            float targetAbsoluteX = centerLeft + targetInstance.X;

            foreach (var kvp in targetInstance.ConnectedParameters) {
                int paramIndex = kvp.Key;
                FunctoidParameter parameter = kvp.Value;

                if (parameter.SourceType == ParameterSourceType.Unlinked) continue;

                // 1. Calculate Target Coordinates (Left edge of the box, centered vertically inside its assigned slot sector)
                float targetX = targetAbsoluteX;
                float targetY = targetInstance.Y + (paramIndex * sectorHeight) + (sectorHeight / 2f);

                float sourceX = 0f;
                float sourceY = 0f;
                bool validSourceFound = false;

                // 2. Resolve Source Coordinates based on connection origin variant
                if (parameter.SourceType == ParameterSourceType.SourceSchemaNode) {
                    // Find where the tree node layout tracker recorded its coordinates during the tree draw pass
                    SchemaNode? schemaNode = FindNodeByPath(SourceRoot, parameter.SourceNodePath);
                    if (schemaNode != null) {
                        sourceX = leftTreeWidth - 16f; // Origin point matching the tree layout anchor bounds
                        sourceY = schemaNode.LastRenderedY;
                        validSourceFound = true;
                    }
                } else if (parameter.SourceType == ParameterSourceType.FunctoidOutput) {
                    // Find the predecessor block on the active visual surface
                    var sourceInstance = ActiveFunctoids.FirstOrDefault(f => f.Id == parameter.SourceFunctoidId);
                    if (sourceInstance != null) {
                        sourceX = centerLeft + sourceInstance.X + sourceInstance.Width; // Originates at the center-right boundary
                        sourceY = sourceInstance.Y + (sourceInstance.Height / 2f);
                        validSourceFound = true;
                    }
                }

                // 3. Draw the Cubic Bezier Routing Wire
                if (validSourceFound) {
                    using var path = new SKPath();
                    path.MoveTo(sourceX, sourceY);

                    // Calculate smooth visual control vectors based on layout distance
                    float controlOffset = Math.Max(30f, Math.Abs(targetX - sourceX) * 0.5f);
                    path.CubicTo(
                        sourceX + controlOffset, sourceY, // First Bezier handle
                        targetX - controlOffset, targetY, // Second Bezier handle
                        targetX, targetY                  // Destination
                    );

                    canvas.DrawPath(path, wirePaint);

                    // Draw tiny terminal joint nubs for crisp look and feel
                    canvas.DrawCircle(sourceX, sourceY, 3f, jointPaint);
                    canvas.DrawCircle(targetX, targetY, 3f, jointPaint);
                }
            }
        }
    }
    private void RenderActiveGridFunctoids(SKCanvas canvas, float canvasLeftOffset) {
        using var borderPaint = new SKPaint { Color = new SKColor(170, 185, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
        using var textPaint = new SKPaint { Color = new SKColor(45, 55, 65), TextSize = 11f, IsAntialias = true, FakeBoldText = true, TextAlign = SKTextAlign.Center };
        using var pinPaint = new SKPaint { Color = new SKColor(130, 140, 150), Style = SKPaintStyle.Fill, IsAntialias = true };

        foreach (var functoid in ActiveFunctoids) {
            if (functoid == null) continue;

            float absoluteX = canvasLeftOffset + functoid.X;
            SKRect rect = new SKRect(absoluteX, functoid.Y, absoluteX + functoid.Width, functoid.Y + functoid.Height);

            // 1. Resolve Dynamic Category Identity Color Tint
            SKColor nodeFillColor = new SKColor(235, 240, 248); // High-contrast neutral safe fallback
            SKColor accentColor = SKColors.LightGray;
            bool hasAccent = false;

            if (functoid.Definition != null) {
                var category = FunctoidCategories.Find(c => c.Id == functoid.Definition.CategoryId);
                if (category != null) {
                    accentColor = GetCategoryColor(category.Color);
                    // Apply a transparent alpha blend (40 out of 255) for the block body 
                    // to maintain crisp text legibility against dark category definitions
                    nodeFillColor = new SKColor(accentColor.Red, accentColor.Green, accentColor.Blue, 40);
                    hasAccent = true;
                }
            }

            // 2. Render Node Base Container 
            using var dynamicBoxPaint = new SKPaint { Color = nodeFillColor, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawRoundRect(rect, 4f, 4f, dynamicBoxPaint);
            canvas.DrawRoundRect(rect, 4f, 4f, borderPaint);

            // --- SELECTION HIGHLIGHT PROFILE ---
            // If this block matches the active click selection, draw an outer focus halo
            if (functoid == selectedCanvasInstance) {
                using var selectionRingPaint = new SKPaint {
                    Color = SKColors.Orange, // Synced with mapping wire theme
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2.5f,
                    IsAntialias = true
                };
                // Slightly expand outward so the ring frames the border without overlapping text
                SKRect selectionRect = rect;
                selectionRect.Inflate(1.5f, 1.5f);
                canvas.DrawRoundRect(selectionRect, 5f, 5f, selectionRingPaint);
            }

            // 3. Render Solid Category Accent Strip (Left Side Frame Flag)
            if (hasAccent) {
                using var accentPaint = new SKPaint { Color = accentColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                SKRect accentStrip = new SKRect(rect.Left, rect.Top, rect.Left + 5f, rect.Bottom);
                canvas.DrawRoundRect(accentStrip, 4f, 4f, accentPaint);
                // Squaring out the inside corners of our accent bar frame safely
                canvas.DrawRect(new SKRect(rect.Left + 3f, rect.Top, rect.Left + 5f, rect.Bottom), accentPaint);
            }

            // 4. Draw Input/Output Handle Vector Nubs
            canvas.DrawCircle(rect.Left, rect.MidY, 4f, pinPaint);
            canvas.DrawCircle(rect.Right, rect.MidY, 4f, pinPaint);

            // 5. Draw Descriptive Label
            string displayName = functoid.Definition != null ? functoid.Definition.Name : "Unknown";
            canvas.DrawText(displayName, rect.MidX + (hasAccent ? 2.5f : 0f), rect.MidY + 4f, textPaint);

            // 6. INDICATOR ONLY: Draw a simple red dot in the top-right corner if custom
            bool isScriptCustomized = functoid.Definition != null &&
                                      !string.IsNullOrEmpty(functoid.CustomScriptBody) &&
                                      functoid.CustomScriptBody.Trim() != functoid.Definition.ScriptTemplate?.Trim();

            if (isScriptCustomized) {
                float dotRadius = 3.5f;
                float dotX = rect.Right - 6f;
                float dotY = rect.Top + 6f;

                using var redDotPaint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawCircle(dotX, dotY, dotRadius, redDotPaint);
            }
        }
    }
    #region RenderFunctoidToolPallete 534
    private void RenderFunctoidToolPalette(SKCanvas canvas) {
        using var bodyPaint = new SKPaint { Color = new SKColor(240, 243, 248), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var headerPaint = new SKPaint { Color = new SKColor(52, 73, 94), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var actionBarPaint = new SKPaint { Color = new SKColor(44, 62, 80), Style = SKPaintStyle.Fill, IsAntialias = true }; // Slightly darker accent for Row 2
        using var borderPaint = new SKPaint { Color = new SKColor(44, 62, 80), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        using var titleTextPaint = new SKPaint { Color = SKColors.White, TextSize = 13f, IsAntialias = true, FakeBoldText = true };
        using var actionTextPaint = new SKPaint { Color = new SKColor(200, 214, 229), TextSize = 11f, IsAntialias = true, FakeBoldText = false };
        using var toggleTextPaint = new SKPaint { Color = SKColors.White, TextSize = 14f, IsAntialias = true, TextAlign = SKTextAlign.Center };

        using var iconStrokePaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        using var iconFillPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };

        SKColor saveIconColor = isCanvasDirty ? new SKColor(231, 76, 60) : new SKColor(52, 152, 219);
        using var saveIconStrokePaint = new SKPaint { Color = saveIconColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        using var saveIconFillPaint = new SKPaint { Color = saveIconColor, Style = SKPaintStyle.Fill, IsAntialias = true };

        using var categoryBgPaint = new SKPaint { Color = new SKColor(215, 222, 233), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var categoryTextPaint = new SKPaint { Color = new SKColor(60, 70, 80), TextSize = 11f, IsAntialias = true, FakeBoldText = true };
        using var itemBoxPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var itemBorderPaint = new SKPaint { Color = new SKColor(195, 205, 215), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
        using var itemTextPaint = new SKPaint { Color = new SKColor(40, 40, 40), TextSize = 11f, IsAntialias = true };

        renderedItemHitboxes.Clear();

        // Adjust height boundary box if collapsed
        float currentHeight = isPaletteExpanded ? expandedPaletteHeight : PaletteHeaderHeight;
        paletteBounds = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + currentHeight);

        if (isPaletteExpanded) {
            canvas.DrawRoundRect(paletteBounds, 6f, 6f, bodyPaint);
        }

        // =================================================================================
        // DRAW TWO-LINE HEADER BACKGROUNDS
        // =================================================================================
        var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);
        canvas.DrawRoundRect(headerRect, 6f, 6f, headerPaint);

        // Render Row 2's distinct sub-action bar background band
        float row1Height = 32f;
        var row2Rect = new SKRect(paletteBounds.Left, paletteBounds.Top + row1Height, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);
        canvas.DrawRect(row2Rect, actionBarPaint);

        // Render Window Title on Row 1
        canvas.DrawText("Functoid Toolbox", paletteBounds.Left + 12f, paletteBounds.Top + 21f, titleTextPaint);

        // =================================================================================
        // ROW 1 BUTTON LAYOUT ENGINE (Right-to-Left Window Level Actions)
        // =================================================================================
        float currentButtonRightEdge = paletteBounds.Right - 12f;
        float row1CenterY = paletteBounds.Top + (row1Height / 2f);

        // 1. Collapse/Expand Toggle Slot
        float toggleX = currentButtonRightEdge - 8f;
        string toggleGlyph = isPaletteExpanded ? "▲" : "▼";
        canvas.DrawText(toggleGlyph, toggleX, paletteBounds.Top + 22f, toggleTextPaint);

        currentButtonRightEdge -= 28f;

        // 2. Save Button Slot (Floppy Disk)
        saveBtnRect = new SKRect(currentButtonRightEdge - 10f, row1CenterY - 10f, currentButtonRightEdge + 10f, row1CenterY + 10f);
        canvas.DrawRect(saveBtnRect, saveIconStrokePaint);
        canvas.DrawRect(new SKRect(currentButtonRightEdge - 5f, saveBtnRect.Top, currentButtonRightEdge + 5f, saveBtnRect.Top + 6f), saveIconFillPaint);
        canvas.DrawRect(new SKRect(currentButtonRightEdge - 4f, saveBtnRect.Bottom - 6f, currentButtonRightEdge + 4f, saveBtnRect.Bottom), saveIconStrokePaint);

        currentButtonRightEdge -= 32f;

        // 3. Load Button Slot (Folder)
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

        // 4. Clear Canvas Button Slot (Trash Can)
        clearBtnRect = new SKRect(currentButtonRightEdge - 9f, row1CenterY - 9f, currentButtonRightEdge + 9f, row1CenterY + 9f);
        canvas.DrawRect(new SKRect(clearBtnRect.Left + 2f, clearBtnRect.Top + 4f, clearBtnRect.Right - 2f, clearBtnRect.Bottom), iconStrokePaint);
        canvas.DrawLine(clearBtnRect.Left, clearBtnRect.Top + 3f, clearBtnRect.Right, clearBtnRect.Top + 3f, iconStrokePaint);
        canvas.DrawRect(new SKRect(currentButtonRightEdge - 3f, clearBtnRect.Top, currentButtonRightEdge + 3f, clearBtnRect.Top + 3f), iconStrokePaint);

        // =================================================================================
        // ROW 2 ACTION LAYOUT ENGINE (Dedicated Transformation Row)
        // =================================================================================
        if (isPaletteExpanded) {
            float row2CenterY = paletteBounds.Top + row1Height + (row1Height / 2f);
            float row2LeftX = paletteBounds.Left + 12f;

            // 5. Execute XSLT Transform Button Slot (Gear Vector Shape)
            executeXsltBtnRect = new SKRect(row2LeftX, row2CenterY - 9f, row2LeftX + 18f, row2CenterY + 9f);
            float gearCenterX = executeXsltBtnRect.MidX;

            canvas.DrawCircle(gearCenterX, row2CenterY, 4f, iconStrokePaint);
            for (int i = 0; i < 8; i++) {
                canvas.Save();
                canvas.RotateDegrees(i * 45f, gearCenterX, row2CenterY);
                canvas.DrawRect(new SKRect(gearCenterX - 1.5f, row2CenterY - 9f, gearCenterX + 1.5f, row2CenterY - 6f), iconFillPaint);
                canvas.Restore();
            }

            // Render complementary label context beside the gear icon
            canvas.DrawText("Execute Map Transformation Pipeline", executeXsltBtnRect.Right + 8f, row2CenterY + 4f, actionTextPaint);
        }

        // =================================================================================
        // RENDER CONTENS (Rest of the list view tracking logic stays intact)
        // =================================================================================
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
            SKRect contentClipArea = new SKRect(
                paletteBounds.Left + 2f,
                paletteBounds.Top + PaletteHeaderHeight + 2f,
                paletteBounds.Right - 2f,
                paletteBounds.Bottom - 4f
            );
            canvas.ClipRect(contentClipArea);

            float startYPosition = paletteBounds.Top + PaletteHeaderHeight + 8f;
            float virtualTrackingY = startYPosition + toolboxVirtualScrollY;

            foreach (var category in FunctoidCategories) {
                SKRect catHeaderRect = new SKRect(leftPadding, virtualTrackingY, leftPadding + usableItemWidth, virtualTrackingY + categoryHeaderHeight);
                category.LastRenderedHeaderBounds = catHeaderRect;

                if (catHeaderRect.Bottom > contentClipArea.Top && catHeaderRect.Top < contentClipArea.Bottom) {
                    canvas.DrawRoundRect(catHeaderRect, 3f, 3f, categoryBgPaint);
                    string arrowToken = category.IsExpanded ? "▼ " : "► ";
                    canvas.DrawText($"{arrowToken}{category.Name.ToUpper()}", catHeaderRect.Left + 8f, catHeaderRect.Top + 15f, categoryTextPaint);
                }

                virtualTrackingY += categoryHeaderHeight + 4f;

                if (category.IsExpanded) {
                    foreach (var functoid in AvailableFunctoids) {
                        if (functoid.CategoryId != category.Id) continue;

                        SKRect rowRect = new SKRect(leftPadding + 6f, virtualTrackingY, leftPadding + usableItemWidth - 6f, virtualTrackingY + itemHeight);
                        renderedItemHitboxes[functoid] = rowRect;

                        if (rowRect.Bottom > contentClipArea.Top && rowRect.Top < contentClipArea.Bottom) {
                            canvas.DrawRoundRect(rowRect, 3f, 3f, itemBoxPaint);
                            canvas.DrawRoundRect(rowRect, 3f, 3f, itemBorderPaint);

                            using var dotPaint = new SKPaint { Color = GetCategoryColor(category.Color), Style = SKPaintStyle.Fill, IsAntialias = true };
                            canvas.DrawCircle(rowRect.Left + 12f, rowRect.MidY, 4f, dotPaint);

                            canvas.DrawText(functoid.Name, rowRect.Left + 24f, rowRect.Top + 16f, itemTextPaint);
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
    #endregion 700
    private void RenderMapConnections(SKCanvas canvas) {
        using var linePaint = new SKPaint {
            Color = new SKColor(46, 204, 113), // Crisp BizTalk Green
            StrokeWidth = 2.0f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        float centerLeft = leftTreeWidth;

        foreach (var conn in Connections) {
            float startX = 0f, startY = 0f;
            float endX = 0f, endY = 0f;

            // 1. EVALUATE START POINT (Where is the line coming from?)
            // ALIGNED: Using ConnectionEndpointType.Functoid from MappingProject.cs
            if (conn.Source.Type == ConnectionEndpointType.Functoid && conn.Source.FunctoidInstanceId != null) {

                // --- REVISED: Use LINQ FirstOrDefault instead of List.Find ---
                var instance = ActiveFunctoids.FirstOrDefault(f => f.Id == conn.Source.FunctoidInstanceId);
                if (instance != null) {
                    startX = centerLeft + instance.X + instance.Width; // Starts at right edge of functoid
                    startY = instance.Y + (instance.Height / 2f);
                } else continue;
            } else // Fallback: It's a Source Tree Node
              {
                startY = FindNodeY(SourceRoot, conn.Source.NodePath);
                startX = leftTreeWidth - 25f; // Center-right of source node row
                if (startY == -1) continue;
            }

            // 2. EVALUATE END POINT (Where is the line going to?)
            // ALIGNED: Using ConnectionEndpointType.Functoid from MappingProject.cs
            if (conn.Target.Type == ConnectionEndpointType.Functoid && conn.Target.FunctoidInstanceId != null) {
                // --- REVISED: Use LINQ FirstOrDefault instead of List.Find ---
                var instance = ActiveFunctoids.FirstOrDefault(f => f.Id == conn.Target.FunctoidInstanceId);
                if (instance != null) {
                    endX = centerLeft + instance.X; // Ends at left edge of functoid
                    endY = instance.Y + (instance.Height / 2f);
                } else continue;
            } else // Fallback: It's a Destination Tree Node
              {
                endY = FindNodeY(DestinationRoot, conn.Target.NodePath);
                endX = Width - rightTreeWidth + 5f; // Left edge of destination node row
                if (endY == -1) continue;
            }

            // 3. DRAW CURVE
            using var path = new SKPath();
            path.MoveTo(startX, startY);

            float controlOffset = Math.Max(30f, Math.Abs(endX - startX) * 0.5f);
            path.CubicTo(startX + controlOffset, startY, endX - controlOffset, endY, endX, endY);
            canvas.DrawPath(path, linePaint);
        }
    }
    #endregion Paint and Render methods
    private SKColor GetCategoryColor(string colorName) {
        if (string.IsNullOrEmpty(colorName)) return SKColors.LightGray;

        // Resolves standard system web color signatures dynamically (e.g., "Brown", "LightBlue")
        System.Drawing.Color systemColor = System.Drawing.Color.FromName(colorName);

        // Check if the name returned a valid known color structure; otherwise, apply fallback
        if (systemColor.IsKnownColor) {
            return new SKColor(systemColor.R, systemColor.G, systemColor.B, systemColor.A);
        }

        return SKColors.LightGray;
    }
    private float FindNodeY(SchemaNode? root, string path) {
        if (root == null) return -1;
        if (root.Name == path) return root.LastRenderedY;

        foreach (var child in root.Children) {
            float found = FindNodeY(child, path);
            if (found != -1) return found;
        }
        return -1;
    }
    private SchemaNode? FindNodeAtPosition(SchemaNode node, float mouseCodeY) {
        float halfHeight = node.LastRenderedHeight / 2f;
        if (mouseCodeY >= node.LastRenderedY - halfHeight && mouseCodeY <= node.LastRenderedY + halfHeight) {
            return node;
        }

        if (node.IsExpanded) {
            foreach (var child in node.Children) {
                var found = FindNodeAtPosition(child, mouseCodeY);
                if (found != null) return found;
            }
        }
        return null;
    }
    private SchemaNode? FindDestinationNodeAtPosition(SchemaNode node, float mouseCodeY) {
        float halfHeight = node.LastRenderedHeight / 2f;
        if (mouseCodeY >= node.LastRenderedY - halfHeight && mouseCodeY <= node.LastRenderedY + halfHeight) {
            return node;
        }

        if (node.IsExpanded) {
            foreach (var child in node.Children) {
                var found = FindDestinationNodeAtPosition(child, mouseCodeY);
                if (found != null) return found;
            }
        }
        return null;
    }
    #region  Modularized MouseDown  Intercept Engine Block Helpers

    private void ProcessLeftClickToolPalette(MouseEventArgs e) {
        var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);

        // --- SUB-ZONE ROUTING: HEADER TRAPS ---
        if (headerRect.Contains(e.X, e.Y)) {
            switch (e.Location) {
                case var loc when executeXsltBtnRect.Contains(loc.X, loc.Y):
                    BtnExecuteTransform_Click(this, EventArgs.Empty);
                    return;

                case var loc when clearBtnRect.Contains(loc.X, loc.Y):
                    var choice = MessageBox.Show(
                        "Are you sure you want to completely clear the canvas? All functoids and mapping traces will be deleted.",
                        "Clear Canvas",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );
                    if (choice == DialogResult.Yes) {
                        ActiveFunctoids.Clear();
                        Connections.Clear();
                        dragSource = null;
                        draggingCanvasInstance = null;
                        isCanvasDirty = false;
                        Invalidate();
                    }
                    return;

                case var loc when saveBtnRect.Contains(loc.X, loc.Y):
                    using (var sfd = new SaveFileDialog { Filter = "XML Files (*.xml)|*.xml", DefaultExt = "xml", AddExtension = true, Title = "Save Mapping Schema" }) {
                        if (sfd.ShowDialog() == DialogResult.OK) {
                            File.WriteAllText(sfd.FileName, SaveConfiguration());
                            isCanvasDirty = false;
                            Invalidate();
                        }
                    }
                    return;

                case var loc when loadBtnRect.Contains(loc.X, loc.Y):
                    using (var ofd = new OpenFileDialog { Filter = "XML Files (*.xml)|*.xml", DefaultExt = "xml", Title = "Load Mapping Schema" }) {
                        if (ofd.ShowDialog() == DialogResult.OK) {
                            LoadConfiguration(File.ReadAllText(ofd.FileName));
                            isCanvasDirty = false;
                            this.FindForm().Text = $"{this.FindForm().Text} -  Working on {ofd.FileName}";


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

        // --- SUB-ZONE ROUTING: EXPANDED BODY LIST INTERACTION ---
        if (isPaletteExpanded) {
            float viewableHeight = paletteBounds.Height - PaletteHeaderHeight - 12f;

            // A. Virtual Scrollbar Track Check
            if (maxToolboxContentHeight > viewableHeight) {
                float scrollbarWidth = 5f;
                float trackLeft = paletteBounds.Right - scrollbarWidth - 6f;
                float trackTop = paletteBounds.Top + PaletteHeaderHeight + 6f;
                SKRect scrollHitRect = new SKRect(trackLeft - 4f, trackTop, paletteBounds.Right, trackTop + viewableHeight);
                if (scrollHitRect.Contains(e.X, e.Y)) {
                    isDraggingToolboxScrollbar = true;
                    toolboxScrollbarDragStartY = e.Y;
                    toolboxScrollbarDragStartScrollY = toolboxVirtualScrollY;
                    Capture = true;
                    return;
                }
            }

            // B. Category Expand/Collapse Accordion Header Strip Checks
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

            // C. Row Grab Item Drag Handlers
            foreach (var itemEntry in renderedItemHitboxes) {
                if (itemEntry.Value.Contains(e.X, e.Y)) {
                    draggingPaletteFunctoid = itemEntry.Key;
                    paletteItemDragOffset = new PointF(55f, 18f); // Center node snapping placement offset
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
            SKRect itemRect = new SKRect(absoluteX, instance.Y, absoluteX + instance.Width, instance.Y + instance.Height);

            if (itemRect.Contains(e.X, e.Y)) {
                if (e.X > itemRect.MidX) { // Right Side: Map connection wire drag initialization
                    dragSource = new ConnectionEndpoint {
                        Type = ConnectionEndpointType.Functoid,
                        FunctoidInstanceId = instance.Id
                    };
                    currentDragPoint = new PointF(itemRect.Right, itemRect.MidY);
                } else { // Left Side: Interactive canvas repositioning displacement drag
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

    private bool ProcessLeftClickSourceTree(MouseEventArgs e) {
        SchemaNode? targetedNode = FindNodeAtPosition(SourceRoot, e.Y);
        if (targetedNode != null) {
            dragSource = new ConnectionEndpoint {
                Type = ConnectionEndpointType.SourceNode,
                NodePath = targetedNode.Name
            };
            currentDragPoint = new PointF(leftTreeWidth - 25f, targetedNode.LastRenderedY);
            Capture = true;
            Invalidate();
            return true;
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
            SKRect itemRect = new SKRect(absoluteX, instance.Y, absoluteX + instance.Width, instance.Y + instance.Height);

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

    #endregion Modularized MouseDown  Intercept Engine Block Helpers
    protected override void OnMouseDown(MouseEventArgs e) {
        lastMousePos = new PointF(e.X, e.Y);
        this.Focus(); // Retain instant canvas keyboard capture
        // Calculate structural metrics used across layout zones
        float mainContentHeight = Height - logPanelHeight;
        float centerLeft = leftTreeWidth;
        float centerRight = Width - rightTreeWidth;
        switch (e.Button) {
            case MouseButtons.Left:
                // 1. Priority Intercept: Tool Palette Floating Overlay Window
                if (paletteBounds.Contains(e.X, e.Y)) {
                    ProcessLeftClickToolPalette(e);
                    return;
                }

                // 2. Priority Intercept: Canvas Interactive Functoid Instances
                if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {
                    if (ProcessLeftClickCanvasFunctoids(e, centerLeft)) return;
                }

                // 3. Priority Intercept: Source Schema Tree Panel Nodes
                if (e.X < leftTreeWidth && e.Y < mainContentHeight && SourceRoot != null) {
                    if (ProcessLeftClickSourceTree(e)) return;
                }

                // 4. Priority Intercept: Resizing UI Layout Splitter Slices
                if (ProcessLeftClickLayoutSplitters(e, mainContentHeight, centerLeft, centerRight)) {
                    return;
                }

                // 5. Fallback: Edge-Wire Connection Trace Intersection Selection
                ProcessLeftClickWireSelection(e);
                break;

            case MouseButtons.Right:
                // Drop out completely if context click happens inside floating tool window container
                if (paletteBounds.Contains(e.X, e.Y)) return;

                // 1. Right Click: Canvas Functoid Nodes Context Menu
                if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {
                    if (ProcessRightClickCanvasFunctoids(e, centerLeft)) return;
                }

                // 2. Right Click: Vector Wire Connection Lines Context Menu
                ProcessRightClickWireSelection(e);
                break;
        }
    }
    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Delete) {
            // CASE A: A Canvas Functoid Block is targeted for deletion
            if (selectedCanvasInstance != null) {
                // 1. Cascade delete all wires tied to this specific instance ID
                string targetId = selectedCanvasInstance.Id.ToString();
                // --- REVISED FOR LINE 1012: Safe lookup for ObservableCollection ---
                var connectionsToRemove = Connections.Where(c =>
                    (c.Source?.Type == ConnectionEndpointType.Functoid && c.Source.FunctoidInstanceId?.ToString() == targetId) ||
                    (c.Target?.Type == ConnectionEndpointType.Functoid && c.Target.FunctoidInstanceId?.ToString() == targetId)
                ).ToArray();

                foreach (var connection in connectionsToRemove) {
                    Connections.Remove(connection);
                }

                // 2. Remove the block itself from the active list
                ActiveFunctoids.Remove(selectedCanvasInstance);

                // 3. Clean up tracking states safely
                if (contextTargetInstance == selectedCanvasInstance) contextTargetInstance = null;
                selectedCanvasInstance = null;

                Invalidate();
                e.Handled = true;
                return;
            }

            // CASE B: A Wire Trace is targeted for deletion (Our existing logic)
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

        // 1. Handle Active Component Dragging Layouts
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
            Invalidate();
            return;
        } else if (isResizingLeft) {
            leftTreeWidth = e.X;
            Invalidate();
            return;
        } else if (isResizingRight) {
            rightTreeWidth = Width - e.X;
            Invalidate();
            return;
        }

        // 2. Evaluate Dynamic Hover Cursor Styles
        var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);

        if (headerRect.Contains(e.X, e.Y)) {
            // HOVER OVERRIDE: If over utility buttons or the collapse toggle region, use normal pointer
            if (clearBtnRect.Contains(e.X, e.Y) ||
                loadBtnRect.Contains(e.X, e.Y) ||
                saveBtnRect.Contains(e.X, e.Y) ||
                e.X > paletteBounds.Right - 28f) {
                Cursor = Cursors.Default;
            } else {
                Cursor = Cursors.SizeAll; // Dragging space for the title block window
            }
        } else if (Math.Abs(e.Y - mainContentHeight) < SplitterWidth) {
            Cursor = Cursors.HSplit;
        } else if (Math.Abs(e.X - leftTreeWidth) < SplitterWidth || Math.Abs(e.X - (Width - rightTreeWidth)) < SplitterWidth) {
            Cursor = Cursors.VSplit;
        } else {
            Cursor = Cursors.Default;
        }
    }
    private static string BuildDefaultScriptFromBodyTemplate(FunctoidDefinition def, string methodName) {
        if (string.IsNullOrWhiteSpace(def.ScriptTemplate)) {
            return $"public object {methodName}(params object[] args) \r\n{{\r\n    return null;\r\n}}";
        }

        // Check if the script template is already a full method signature definition
        if (def.ScriptTemplate.Contains("public ") || def.ScriptTemplate.Contains("private ")) {
            return def.ScriptTemplate;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"public object {methodName}(object param0, object param1 = null, object param2 = null)");
        sb.AppendLine("{");

        // Process formatting variables or simple joining symbols safely
        if (!string.IsNullOrWhiteSpace(def.JoinOperator)) {
            sb.AppendLine($"    var parts = new[] {{ param0, param1, param2 }}.Where(p => p != null).Select(p => p.ToString());");
            sb.AppendLine($"    return string.Join(\"{def.JoinOperator}\", parts);");
        } else {
            // CRITICAL FIX: Safe token replacement that doesn't blow up if {1} or {2} are missing
            string processedTemplate = def.ScriptTemplate;
            processedTemplate = processedTemplate.Replace("{0}", "param0");
            processedTemplate = processedTemplate.Replace("{1}", "param1");
            processedTemplate = processedTemplate.Replace("{2}", "param2");

            sb.AppendLine($"    {processedTemplate}");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }
    private static string ExtractMethodNameFromTemplate(string template, string functoidName, int functoidId) {
        if (string.IsNullOrWhiteSpace(template)) {
            // Fallback clean name alpha-numeric format strip
            return $"Transform_{functoidName.Replace(" ", "")}_{functoidId}";
        }

        try {
            // Look for common patterns like "public string StringLeft(" or "public double Add("
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
        } catch {
            // Avoid crash on malformed edge-cases
        }

        return $"Transform_{functoidName.Replace(" ", "")}_{functoidId}";
    }
    protected override void OnMouseUp(MouseEventArgs e) {
        float centerLeft = leftTreeWidth;
        float centerRight = Width - rightTreeWidth;
        float mainContentHeight = Height - logPanelHeight;

        // --- 1. PALETTE DROP HANDLER: Add new Functoid to Workspace Canvas ---
        // --- 1. PALETTE DROP HANDLER: Add new Functoid to Workspace Canvas ---
        if (draggingPaletteFunctoid != null) {
            if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {

                float localX = (e.X - centerLeft) - paletteItemDragOffset.X;
                float localY = e.Y - paletteItemDragOffset.Y;

                if (localX < 0) localX = 0;
                if (localX + 110f > (centerRight - centerLeft)) localX = (centerRight - centerLeft) - 110f;
                if (localY < 0) localY = 0;
                if (localY + 36f > mainContentHeight) localY = mainContentHeight - 36f;

                // FIX: Extract clean function name from the ScriptTemplate content
                // e.g. "public string StringLeft(...)" -> target name is "StringLeft"
                string defaultMethodName = ExtractMethodNameFromTemplate(
                    draggingPaletteFunctoid.ScriptTemplate,
                    draggingPaletteFunctoid.Name,
                    draggingPaletteFunctoid.Id
                );

                var newInstance = new FunctoidInstance {
                    Id = Guid.NewGuid(),
                    X = localX,
                    Y = localY,
                    Width = 110f,
                    Height = 36f,
                    Definition = draggingPaletteFunctoid,

                    // CRITICAL FIX: Pull your complete CDATA script straight out of the XML configuration
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
        // --- 2. MAP CONNECTION TRACE WIRE HANDLER ---
        if (dragSource != null) {
            ConnectionEndpoint? dragTarget = null;

            // EVALUATE TARGET: Dropped onto a Destination Tree Node?
            if (e.X > centerRight && e.Y < mainContentHeight && DestinationRoot != null) {
                SchemaNode? targetedNode = FindDestinationNodeAtPosition(DestinationRoot, e.Y);
                if (targetedNode != null) {
                    dragTarget = new ConnectionEndpoint {
                        Type = ConnectionEndpointType.DestinationNode,
                        NodePath = targetedNode.Name
                    };
                }
            }
            // EVALUATE TARGET: Dropped onto a Canvas Functoid?
            else if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {
                foreach (var instance in ActiveFunctoids) {
                    float absoluteX = centerLeft + instance.X;
                    SKRect itemRect = new SKRect(absoluteX, instance.Y, absoluteX + instance.Width, instance.Y + instance.Height);

                    // Clicking/releasing on the left half represents an Input connection
                    if (itemRect.Contains(e.X, e.Y) && e.X <= itemRect.MidX) {
                        dragTarget = new ConnectionEndpoint {
                            Type = ConnectionEndpointType.Functoid,
                            FunctoidInstanceId = instance.Id
                        };
                        break;
                    }
                }
            }

            // VALIDATE AND COMMIT CONNECTION
            if (dragTarget != null) {
                bool isSelfLoop = dragSource.Type == ConnectionEndpointType.Functoid &&
                                  dragTarget.Type == ConnectionEndpointType.Functoid &&
                                  dragSource.FunctoidInstanceId == dragTarget.FunctoidInstanceId;

                if (!isSelfLoop) {
                    // Multi-connection constraint lifted: we bypass duplication filters entirely
                    Connections.Add(new MappingConnection {
                        Source = dragSource,
                        Target = dragTarget
                    });
                }
            }

            dragSource = null;
            Capture = false;
            Invalidate();
        }

        // --- 3. RESET MISCELLANEOUS RESIZE TRACKERS ---
        draggingCanvasInstance = null;
        isDraggingPalette = false;
        isResizingLog = false;
        isResizingLeft = false;
        isResizingRight = false;
    }
    /// <summary>
    /// Serializes the active canvas functoids and wire links into an XML string.
    /// </summary>
    public string SaveConfiguration() {
        var state = new MappingProjectState {
            // --- REVISED: Extract data as standard Lists for serialization compatibility ---
            ActiveFunctoids = this.ActiveFunctoids.ToList(),
            Connections = this.Connections.ToList()
        };
        var serializer = new XmlSerializer(typeof(MappingProjectState));
        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = true });
        serializer.Serialize(xmlWriter, state);
        return stringWriter.ToString();
    }
    /// <summary>
    /// Deserializes an XML map configuration string and completely rehydrates the canvas layout.
    /// </summary>
    public void LoadConfiguration(string xmlContent) {
        if (string.IsNullOrWhiteSpace(xmlContent)) return;
        try {
            var serializer = new XmlSerializer(typeof(MappingProjectState));
            using var stringReader = new StringReader(xmlContent);
            if (serializer.Deserialize(stringReader) is MappingProjectState loadedState) {
                // Clear existing layout records
                this.ActiveFunctoids.Clear();
                this.Connections.Clear();
                // Rehydrate collections
                if (loadedState.ActiveFunctoids != null) {
                    // --- REVISED: Use a loop to populate the ObservableCollection ---
                    foreach (var functoid in loadedState.ActiveFunctoids) {
                        this.ActiveFunctoids.Add(functoid);
                    }
                }
                if (loadedState.Connections != null) {
                    // Rehydrate the input index lookup safety map
                    foreach (var conn in loadedState.Connections) {
                        if (conn.Target != null) {
                            // Sync our InputIndex property helper back to ArgumentIndex
                            conn.Target.InputIndex = conn.Target.ArgumentIndex;
                        }
                    }
                    if (loadedState.Connections != null) {
                        // --- REVISED: Use a loop to populate the ObservableCollection ---
                        foreach (var connection in loadedState.Connections) {
                            this.Connections.Add(connection);
                        }
                    }
                }
                this.isCanvasDirty = false;
                // Force SkiaSharp to repaint the newly hydrated layout elements immediately
                this.Invalidate();
            }
        } catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine($"[REHYDRATE ERROR] Failed to load layout map XML: {ex.Message}");
            throw;
        }
    }
    public void AssignInputParameter(FunctoidInstance targetFunctoid, ConnectionEndpoint dragSource, float dropY) {
        // 1. Determine how many parameters this functoid can accept
        // (Fallback to 1 if not explicitly declared in your XML definition asset)
        int maxParams = targetFunctoid.Definition?.InputParametersCount ?? 1;
        // 2. Calculate which parameter slot index the user was targeting based on drop height
        float relativeY = dropY - targetFunctoid.Y;
        float sectorHeight = targetFunctoid.Height / maxParams;
        int targetIndex = (int)(relativeY / sectorHeight);
        // Guard bounds safety
        if (targetIndex < 0) targetIndex = 0;
        if (targetIndex >= maxParams) targetIndex = maxParams - 1;
        // 3. Build the parameter assignment descriptor
        var parameterAssignment = new FunctoidParameter {
            Index = targetIndex
        };
        if (dragSource.Type == ConnectionEndpointType.SourceNode) {
            parameterAssignment.SourceType = ParameterSourceType.SourceSchemaNode;
            parameterAssignment.SourceNodePath = dragSource.NodePath;
        } else if (dragSource.Type == ConnectionEndpointType.Functoid) {
            // Prevent cyclic self-linking loops
            if (dragSource.FunctoidInstanceId == targetFunctoid.Id) return;
            parameterAssignment.SourceType = ParameterSourceType.FunctoidOutput;
            parameterAssignment.SourceFunctoidId = dragSource.FunctoidInstanceId;
        }
        // 4. Save or overwrite the slot connection mapping state
        targetFunctoid.ConnectedParameters[targetIndex] = parameterAssignment;
    }
}