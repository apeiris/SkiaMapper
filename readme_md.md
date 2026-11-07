# SkiaMapper Architect's & Technical Reference Guide

A high-performance, hardware-accelerated XML/EDI mapping control built with C#, SkiaSharp, and WinForms.

---

## 🏛️ Architecture & Component Layout

`SkiaMapperControl` relies on a modular, multi-panel design separating canvas interactions, schema navigation, and XSLT compilation:

*   **`SkiaMapperControl`**: Core parent container overseeing workspace canvas rendering, functoid palette overlays, connection wire paths, layout splitters, and high-level mouse capture routing.
*   **`SourceSchemaControl`**: Independent `SKControl` subclass managing source document tree rendering, hierarchy expansion, and node drag initiation.
*   **`DestinationSchemaControl`**: Independent `SKControl` subclass handling target schema navigation, hit testing, and node drop resolution.
*   **`XsltMapGenerator`**: Decoupled XSLT 1.0 compilation engine leveraging Roslyn for dynamic C# functoid assembly generation and relative XPath evaluation.

---

## 🚀 Key Improvements & Technical Revisions

### 1. Segregated Schema Controls
*   Decoupled **`SourceSchemaControl`** and **`DestinationSchemaControl`** into modular, stand-alone custom controls.
*   Encapsulated virtual scrolling, layout padding, and hit-testing mechanics internally within each schema panel.

### 2. WinForms Mouse Capture & Cross-Control Drag-and-Drop
*   **Cross-Control Wire Dragging**: Resolved wire dropping failures across control boundaries by transferring WinForms mouse capture to `this.Parent` (`SkiaMapperControl`) when node drag operations initiate in `SourceSchemaControl`.
*   **Precise Screen-to-Client Coordinate Mapping**: Updated destination drop resolution in `SkiaMapperControl.OnMouseUp` to use `DestinationPanelControl.PointToClient(this.PointToScreen(...))`, guaranteeing accurate hit-testing against `_destinationNodeYCache` regardless of splitter width or window dimensions.

### 3. XSLT Loop Context & Functoid Parameter Preservation
*   **Relative Expression Substitution**: Updated `RenderNodeRecursive` in `XsltMapGenerator` from a strict `StartsWith` check to global string substitution (`.Replace(prefix, "")`).
*   **Loop Context Integrity**: Ensures that both direct paths and functoid extension wrapper calls (e.g., `user:TransformName(string(PID/PID05))`) evaluate relative to the active `xsl:for-each` loop context rather than reverting to absolute `//` document root queries.

### 4. Interactive Header Toolbar Feedback
*   Added user error feedback alerts inside `DestinationSchemaControl` (`OpenDestinationViewer` and `EditDestinationSchema`) when target schema file paths are unassigned or non-existent.

---

## 🛠️ Build & Runtime Requirements

*   **.NET Core / .NET Framework** with WinForms support
*   **SkiaSharp & SkiaSharp.Views.Desktop** for GPU/hardware-accelerated graphics rendering
*   **Microsoft.CodeAnalysis.CSharp (Roslyn)** for runtime C# functoid script compilation