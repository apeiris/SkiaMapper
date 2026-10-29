using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SkiaMapper.Models; // Maps your ConnectionEndpoint and FunctoidInstance models
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace SkiaMapper.Services {
    /// <summary>
    /// Decoupled compilation engine that walks the visual canvas topology backward 
    /// from Destination endpoints using your native ConnectionEndpoint types.
    /// </summary>
    public class XsltMapGenerator {
        private readonly ObservableCollection<FunctoidInstance> _activeFunctoids;
        private readonly ObservableCollection<MappingConnection> _connections;
        // Tracks dynamic fallback parameters globally indexed by [FunctoidInstanceId -> [ParamSlotIndex -> StringLiteralValue]]
        private readonly Dictionary<Guid, Dictionary<int, string>> _instanceFallbackParameterCache;
        public XsltMapGenerator(
            ObservableCollection<FunctoidInstance> activeFunctoids,
            ObservableCollection<MappingConnection> connections) {
            _activeFunctoids = activeFunctoids ?? throw new ArgumentNullException(nameof(activeFunctoids));
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
            _instanceFallbackParameterCache = new Dictionary<Guid, Dictionary<int, string>>();
        }
        public void GenerateAndSaveMap() {
            if (_connections.Count == 0) {
                MessageBox.Show("The workspace canvas contains no connections to compile.", "XSLT Engine Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try {
                _instanceFallbackParameterCache.Clear();
                string xsltOutput = CompileXsltPayload();
                string xsltName = "CanvasMapExport.xslt";
                File.WriteAllText(xsltName, xsltOutput, Encoding.UTF8);
                MainForm.XsltPath = xsltName;
            } catch (Exception ex) {
                MessageBox.Show($"XSLT generation failed:\n{ex.Message}", "Pipeline Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public object BuildDynamicExtensionObject() {
            var codeBuilder = new StringBuilder();
            codeBuilder.AppendLine("using System;");
            codeBuilder.AppendLine("using System.Linq;");
            codeBuilder.AppendLine("using System.Data;");
            codeBuilder.AppendLine("using Microsoft.Data.SqlClient;");
            codeBuilder.AppendLine("namespace SkiaMapper.Dynamic {");
            codeBuilder.AppendLine("    public class FunctoidExtension {");

            var usedFunctoidIds = _connections
                .Where(c => c.Source != null && c.Source.Type == ConnectionEndpointType.Functoid && c.Source.FunctoidInstanceId.HasValue)
                .Select(c => c.Source.FunctoidInstanceId.Value)
                .Distinct()
                .ToList();

            var activeInstances = _activeFunctoids
                .Where(f => usedFunctoidIds.Contains(f.Id) || _connections.Any(c => c.Target?.FunctoidInstanceId == f.Id))
                .ToList();

            var addedSignatures = new HashSet<string>();

            foreach (var functoid in activeInstances) {
                int paramCount = _connections
                    .Where(c => c.Target?.Type == ConnectionEndpointType.Functoid && c.Target.FunctoidInstanceId == functoid.Id)
                    .Select(c => c.Target.InputIndex)
                    .DefaultIfEmpty(-1)
                    .Max() + 1;

                if (paramCount <= 0) {
                    paramCount = functoid.Definition?.InputParametersCount ?? 0;
                }

                string methodName = !string.IsNullOrEmpty(functoid.CustomMethodName)
                    ? functoid.CustomMethodName
                    : (functoid.Definition?.Name?.Replace(" ", "") ?? "Transform");

                string script;
                if (!string.IsNullOrWhiteSpace(functoid.CustomScriptBody)) {
                    script = functoid.CustomScriptBody.Trim();
                } else {
                    var paramList = string.Join(", ", Enumerable.Range(0, Math.Max(1, paramCount)).Select(i => $"object p{i}"));
                    script = $"public static string {methodName}({paramList}) {{ return p0?.ToString() ?? \"\"; }}";
                }

                if (!addedSignatures.Contains(script)) {
                    addedSignatures.Add(script);
                    codeBuilder.AppendLine(script);
                    codeBuilder.AppendLine();
                }
            }

            codeBuilder.AppendLine("    }");
            codeBuilder.AppendLine("}");

            // Parse C# syntax tree
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(codeBuilder.ToString());

            var references = new MetadataReference[]
             {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Data.DataRow).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Microsoft.Data.SqlClient.SqlConnection).Assembly.Location),
        MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
        MetadataReference.CreateFromFile(Assembly.Load("System.ComponentModel.Primitives").Location),
        MetadataReference.CreateFromFile(Assembly.Load("System.ComponentModel.TypeConverter").Location), // Required for DataTable components
        MetadataReference.CreateFromFile(Assembly.Load("System.Xml.ReaderWriter").Location), // Required for dt.WriteXml / XmlWriter
        MetadataReference.CreateFromFile(Assembly.Load("System.Private.Xml").Location) // Required for DataTable serialization XML writing
             };
            var compilation = CSharpCompilation.Create(
                $"FunctoidAssembly_{Guid.NewGuid():N}",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success) {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage());

                throw new InvalidOperationException($"Roslyn Functoid Compilation Failed:\n{string.Join("\n", errors)}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            Assembly assembly = Assembly.Load(ms.ToArray());
            Type type = assembly.GetType("SkiaMapper.Dynamic.FunctoidExtension")!;
            return Activator.CreateInstance(type)!;
        }
        public object BuildDynamicFunctoidExtensionObject() {
            var scriptCode = new StringBuilder();
            scriptCode.AppendLine("using System;");
            scriptCode.AppendLine("namespace SkiaMapper.Dynamic {");
            scriptCode.AppendLine("    public class FunctoidExtension {");

            // Reuse your existing Phase 1 Roslyn logic here to sanitize methods, 
            // but append them into scriptCode instead of the XSLT StringBuilder.

            scriptCode.AppendLine("    }");
            scriptCode.AppendLine("}");

            // Compile into in-memory assembly
            SyntaxTree tree = CSharpSyntaxTree.ParseText(scriptCode.ToString());
            var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
            var compilation = CSharpCompilation.Create("FunctoidAssembly", new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);
            if (!emitResult.Success) {
                throw new InvalidOperationException("Failed to compile functoid scripts.");
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = System.Reflection.Assembly.Load(ms.ToArray());
            var type = assembly.GetType("SkiaMapper.Dynamic.FunctoidExtension")!;
            return Activator.CreateInstance(type)!;
        }

        private string CompileXsltPayload() {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<xsl:stylesheet version=\"1.0\" ");
            sb.AppendLine("                xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"");
            sb.AppendLine("                xmlns:user=\"urn:my-scripts\"");

            // --- DYNAMICALLY DETECT AND REGISTER NAMESPACE PREFIXES ---
            var discoveredPrefixes = new HashSet<string>();
            foreach (var conn in _connections) {
                if (conn.Source?.NodePath != null && conn.Source.NodePath.Contains(':')) {
                    string prefix = conn.Source.NodePath.Split(':')[0];
                    discoveredPrefixes.Add(prefix);
                }
                if (conn.Target?.NodePath != null && conn.Target.NodePath.Contains(':')) {
                    string prefix = conn.Target.NodePath.Split(':')[0];
                    discoveredPrefixes.Add(prefix);
                }
            }

            foreach (var prefix in discoveredPrefixes) {
                sb.AppendLine($"                xmlns:{prefix}=\"http://tempuri.org/{prefix}\"");
            }

            sb.AppendLine("                exclude-result-prefixes=\"user\">");
            sb.AppendLine("  <xsl:output method=\"xml\" indent=\"yes\" encoding=\"utf-8\"/>");
            sb.AppendLine();

            sb.AppendLine("  <xsl:template match=\"/\">");

            var terminalConnections = _connections
                .Where(c => c.Target != null && c.Target.Type == ConnectionEndpointType.DestinationNode)
                .ToList();

            // Build hierarchical tree structure from paths like "po/billto/a1"
            var rootNode = new XsltTreeNode { Name = "Root" };
            foreach (var connection in terminalConnections) {
                string path = connection.Target.NodePath;
                if (string.IsNullOrEmpty(path)) continue;

                string[] segments = path.Split('/');
                var currentNode = rootNode;
                foreach (var segment in segments) {
                    bool isAttr = segment.StartsWith("@");
                    if (!currentNode.Children.TryGetValue(segment, out var child)) {
                        child = new XsltTreeNode { Name = segment, IsAttribute = isAttr };
                        currentNode.Children[segment] = child;
                    }
                    currentNode = child;
                }

                string sourceExpr = TraceSourceEndpoint(connection.Source, out bool isFunctoidCall);
                currentNode.ValueExpression = sourceExpr;



                // --- DYNAMIC CONTAINER LOOP DETECTION ---
                if (path.Contains("Loop") || (connection.Source != null && connection.Source.NodePath != null && connection.Source.NodePath.Contains("Loop"))) {
                    string rawPath = string.Empty;

                    if (connection.Source?.Type == ConnectionEndpointType.SourceNode) {
                        rawPath = $"//{ToValidXPath(connection.Source.NodePath)}";
                    } else if (connection.Source?.Type == ConnectionEndpointType.Functoid) {
                        var srcFunctoid = _activeFunctoids.FirstOrDefault(f => f.Id == connection.Source.FunctoidInstanceId);
                        if (srcFunctoid != null && srcFunctoid.Definition?.Name == "Looping") {
                            rawPath = GetLoopingFunctoidRawPath(srcFunctoid.Id);
                        } else {
                            // Fallback: grab the first source node path feeding this functoid
                            var firstInputWire = _connections.FirstOrDefault(c => c.Target?.FunctoidInstanceId == srcFunctoid?.Id && c.Source?.NodePath != null);
                            if (firstInputWire?.Source != null) {
                                rawPath = $"//{ToValidXPath(firstInputWire.Source.NodePath)}";
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(rawPath)) {
                        // Isolate the Loop container element, e.g. //Loop[@LoopId='N1']
                        int loopIndex = rawPath.LastIndexOf("Loop[@LoopId");
                        if (loopIndex >= 0) {
                            int closingBracket = rawPath.IndexOf(']', loopIndex);
                            if (closingBracket > 0) {
                                rawPath = rawPath.Substring(0, closingBracket + 1);
                            }
                        }
                        currentNode.LoopSourcePath = rawPath;
                    }
                }


            }

            // Recursively render the nested XML structure into the XSLT template
            foreach (var child in rootNode.Children.Values) {
                RenderNodeRecursive(child, sb, 2, null);
            }

            sb.AppendLine("  </xsl:template>");
            sb.AppendLine("</xsl:stylesheet>");

            return sb.ToString();
        }
        private string GetLoopingFunctoidRawPath(Guid functoidId) {
            // Find the wire plugging into input slot 0 of this Looping functoid
            var incomingWire = _connections.FirstOrDefault(c =>
                c.Target?.Type == ConnectionEndpointType.Functoid &&
                c.Target.FunctoidInstanceId == functoidId &&
                c.Target.InputIndex == 0);

            if (incomingWire?.Source != null) {
                if (incomingWire.Source.Type == ConnectionEndpointType.SourceNode && !string.IsNullOrEmpty(incomingWire.Source.NodePath)) {
                    return $"//{ToValidXPath(incomingWire.Source.NodePath)}";
                }
            }
            return string.Empty;
        }

      
        private string ExtractGenericLoopExpression(string sourceExpr) {
            if (string.IsNullOrEmpty(sourceExpr)) return string.Empty;

            // If the expression points to a loop containing [@LoopId='...'], 
            // truncate at the closing bracket to isolate the collection selector (e.g., //Loop[@LoopId='PO1'])
            int bracketIndex = sourceExpr.IndexOf("]");
            if (bracketIndex > 0) {
                return sourceExpr.Substring(0, bracketIndex + 1);
            }

            // Fallback: strip the leaf segment if it's a standard path
            int lastSlash = sourceExpr.LastIndexOf('/');
            if (lastSlash > 0) {
                return sourceExpr.Substring(0, lastSlash);
            }

            return sourceExpr;
        }
        private string ExtractParentLoopExpression(string sourceExpr) {
            // If the source expression targets a specific field inside a loop (e.g. //Loop[@LoopId='PO1']/PO1/PO101),
            // strip the leaf segment to get the repeating container query (e.g. //Loop[@LoopId='PO1'])
            int lastSlash = sourceExpr.LastIndexOf('/');
            if (lastSlash > 0 && !sourceExpr.EndsWith("]")) {
                return sourceExpr.Substring(0, lastSlash);
            }
            return sourceExpr;
        }
        private void RenderNodeRecursive(XsltTreeNode node, StringBuilder sb, int indentLevel, string activeLoopPath = null) {
            string indent = new string(' ', indentLevel * 2);

            // Check if this node introduces a new loop
            bool hasLoop = !string.IsNullOrEmpty(node.LoopSourcePath);
            string currentLoopPath = hasLoop ? node.LoopSourcePath : activeLoopPath;

            if (node.IsAttribute) {
                string attrName = node.Name.TrimStart('@');
                sb.AppendLine($"{indent}<xsl:attribute name=\"{attrName}\">");
                if (!string.IsNullOrEmpty(node.ValueExpression)) {
                    string expr = node.ValueExpression;
                    if (!string.IsNullOrEmpty(activeLoopPath)) {
                        string prefix = activeLoopPath.TrimEnd('/') + "/";
                        if (expr.StartsWith(prefix)) {
                            expr = expr.Substring(prefix.Length);
                        }
                    }
                    sb.AppendLine($"{indent}  <xsl:value-of select=\"{expr}\" />");
                }
                sb.AppendLine($"{indent}</xsl:attribute>");
                return;
            }

            // --- BIZTALK-STYLE <xsl:for-each> RENDERING ---
            if (hasLoop) {
                sb.AppendLine($"{indent}<xsl:for-each select=\"{node.LoopSourcePath}\">");
                indentLevel++;
                indent = new string(' ', indentLevel * 2);
            }

            var attributeChildren = node.Children.Values.Where(c => c.IsAttribute).ToList();
            var elementChildren = node.Children.Values.Where(c => !c.IsAttribute).ToList();

            if (elementChildren.Count == 0 && attributeChildren.Count == 0) {
                if (!string.IsNullOrEmpty(node.ValueExpression)) {
                    string expr = node.ValueExpression;
                    if (!string.IsNullOrEmpty(currentLoopPath)) {
                        string prefix = currentLoopPath.TrimEnd('/') + "/";
                        if (expr.StartsWith(prefix)) {
                            expr = expr.Substring(prefix.Length);
                        }
                    }

                    // Automatically enable disable-output-escaping for function/lookup calls returning XML strings
                    bool isFunctionCall = expr.Contains("(");
                    string doeAttr = isFunctionCall ? " disable-output-escaping=\"yes\"" : "";

                    sb.AppendLine($"{indent}<{node.Name}>");
                    sb.AppendLine($"{indent}  <xsl:value-of select=\"{expr}\"{doeAttr} />");
                    sb.AppendLine($"{indent}</{node.Name}>");
                } else {
                    sb.AppendLine($"{indent}<{node.Name}/>");
                }
            } else {
                sb.AppendLine($"{indent}<{node.Name}>");

                foreach (var attrChild in attributeChildren) {
                    RenderNodeRecursive(attrChild, sb, indentLevel + 1, currentLoopPath);
                }

                foreach (var elemChild in elementChildren) {
                    RenderNodeRecursive(elemChild, sb, indentLevel + 1, currentLoopPath);
                }

                sb.AppendLine($"{indent}</{node.Name}>");
            }

            if (hasLoop) {
                indentLevel--;
                indent = new string(' ', indentLevel * 2);
                sb.AppendLine($"{indent}</xsl:for-each>");
            }
        }
        private class XsltTreeNode {
            public string Name { get; set; } = "";
            public bool IsAttribute { get; set; } = false;
            public Dictionary<string, XsltTreeNode> Children { get; } = new Dictionary<string, XsltTreeNode>();
            public string? ValueExpression { get; set; }
            public string? LoopSourcePath { get; set; } // <--- Added for BizTalk-style looping
        }
        /// <summary>
        /// Recursively walks the canvas graph topology backward to compile properly formatted 
        /// nested function arguments, accounting for literal parameter fallbacks and missing connection slots.
        /// </summary>
        private string TraceSourceEndpoint(ConnectionEndpoint source, out bool isFunctoidCall) {
            isFunctoidCall = false;
            if (source == null) return string.Empty;

            if (source.Type == ConnectionEndpointType.Functoid && source.FunctoidInstanceId.HasValue) {
                isFunctoidCall = true;
                var targetFunctoid = _activeFunctoids.FirstOrDefault(f => f.Id == source.FunctoidInstanceId.Value);
                if (targetFunctoid == null) return "\"\"";

                var inputConnections = _connections
                    .Where(c => c.Target != null &&
                                c.Target.Type == ConnectionEndpointType.Functoid &&
                                c.Target.FunctoidInstanceId == targetFunctoid.Id)
                    .ToList();

                var argumentExpressions = new List<string>();
                int totalExpectedSlots = targetFunctoid.Definition?.InputParametersCount ?? 0;

                if (totalExpectedSlots == 0 && inputConnections.Count > 0) {
                    totalExpectedSlots = inputConnections.Max(c => c.Target.InputIndex) + 1;
                }

                for (int slotIndex = 0; slotIndex < totalExpectedSlots; slotIndex++) {
                    var matchingWire = inputConnections.FirstOrDefault(c => c.Target.InputIndex == slotIndex);

                    if (matchingWire != null && matchingWire.Source != null) {
                        if (matchingWire.Source.Type == ConnectionEndpointType.SourceNode) {
                            // --- SANITIZED HERE ---
                            string cleanPath = ToValidXPath(matchingWire.Source.NodePath);
                            argumentExpressions.Add($"string(//{cleanPath})");
                        } else if (matchingWire.Source.Type == ConnectionEndpointType.Functoid) {
                            string nestedCall = TraceSourceEndpoint(matchingWire.Source, out bool innerIsFunctoid);
                            argumentExpressions.Add(innerIsFunctoid || nestedCall.StartsWith("user:")
                                ? nestedCall
                                : $"string({nestedCall})");
                        }
                    } else {
                        // --- INTEGRATED FALLBACK PARAMETER EVALUATION ENGINE ---
                        if (_instanceFallbackParameterCache.TryGetValue(targetFunctoid.Id, out var customDefaults) &&
                            customDefaults.TryGetValue(slotIndex, out string? assignedDefaultLiteral)) {
                            // Injected via Roslyn extraction logic during the Phase 1 trace loop
                            argumentExpressions.Add(assignedDefaultLiteral);
                        } else if (targetFunctoid.CustomMethodName == "StringLeft" && slotIndex == 1) {
                            argumentExpressions.Add("5"); // Core factory framework baseline
                        } else {
                            argumentExpressions.Add("\"\"");
                        }
                    }
                }

                string joinedArgs = string.Join(", ", argumentExpressions);
                string methodName = !string.IsNullOrEmpty(targetFunctoid.CustomMethodName)
                    ? targetFunctoid.CustomMethodName
                    : (targetFunctoid.Definition?.Name?.Replace(" ", "") ?? "Transform");

                return $"user:{methodName}({joinedArgs})";
            }

            if (source.Type == ConnectionEndpointType.SourceNode && !string.IsNullOrEmpty(source.NodePath)) {
                // --- SANITIZED HERE ---
                string cleanPath = ToValidXPath(source.NodePath);
                return $"//{cleanPath}";
            }

            return string.Empty;
        }
        private string ToValidXPath(string rawPath) {
            if (string.IsNullOrEmpty(rawPath)) return string.Empty;

            // If the path uses the UI display format like "Loop [PO1]", convert it to valid XPath.
            // e.g., "Loop [PO1]" -> "Loop[@LoopId='PO1']" or "Loop_PO1" depending on your XML structure.
            if (rawPath.Contains("Loop [") && rawPath.EndsWith("]")) {
                int startIdx = rawPath.IndexOf("[") + 1;
                int len = rawPath.Length - 1 - startIdx;
                string loopId = rawPath.Substring(startIdx, len).Trim();

                // Find the base segment before "Loop ["
                string basePart = rawPath.Substring(0, rawPath.IndexOf("Loop [")).TrimEnd('/');
                return $"{basePart}/Loop[@LoopId='{loopId}']";
            }

            // General cleanup for any remaining spaces in node segments
            var segments = rawPath.Split('/');
            for (int i = 0; i < segments.Length; i++) {
                if (segments[i].Contains(" [") && segments[i].EndsWith("]")) {
                    // Convert custom display names safely
                    segments[i] = segments[i].Replace(" [", "[@LoopId='").Replace("]", "']");
                } else {
                    segments[i] = segments[i].Replace(" ", "_");
                }
            }
            return string.Join("/", segments);
        }

        private string DetectCommonLoopPath(List<string> sourceExpressions) {
            if (sourceExpressions == null || sourceExpressions.Count == 0) return null;

            // Split all source paths into segments
            var pathSegmentsList = sourceExpressions
                .Select(e => e.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                .ToList();

            // Find the longest common prefix that contains a Loop container
            string commonLoop = null;
            var firstPathSegments = pathSegmentsList[0];

            for (int i = 0; i < firstPathSegments.Length; i++) {
                string candidatePath = "//" + string.Join("/", firstPathSegments.Take(i + 1));

                // Check if this candidate is a parent prefix for all source paths
                bool isCommonToAll = pathSegmentsList.All(segments =>
                    string.Join("/", segments).StartsWith(string.Join("/", firstPathSegments.Take(i + 1))));

                if (isCommonToAll && candidatePath.Contains("Loop")) {
                    commonLoop = candidatePath;
                }
            }

            return commonLoop;
        }
    }
}