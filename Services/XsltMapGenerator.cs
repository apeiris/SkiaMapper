using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SkiaMapper.Models;
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

        /// <summary>
        /// Public helper to retrieve the raw compiled XSLT payload string for file bundling.
        /// </summary>
        public string CompileXsltPayloadPublic() {
            _instanceFallbackParameterCache.Clear();
            return CompileXsltPayload();
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

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(codeBuilder.ToString());

            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Data.DataRow).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Microsoft.Data.SqlClient.SqlConnection).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.ComponentModel.Primitives").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.ComponentModel.TypeConverter").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Xml.ReaderWriter").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Private.Xml").Location)
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

        private string CompileXsltPayload() {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<xsl:stylesheet version=\"1.0\" ");
            sb.AppendLine("                xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"");
            sb.AppendLine("                xmlns:user=\"urn:my-scripts\"");

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

                if (path.Contains("Loop") || (connection.Source != null && connection.Source.NodePath != null && connection.Source.NodePath.Contains("Loop"))) {
                    string rawPath = string.Empty;

                    if (connection.Source?.Type == ConnectionEndpointType.SourceNode) {
                        rawPath = $"//{ToValidXPath(connection.Source.NodePath)}";
                    } else if (connection.Source?.Type == ConnectionEndpointType.Functoid) {
                        var srcFunctoid = _activeFunctoids.FirstOrDefault(f => f.Id == connection.Source.FunctoidInstanceId);
                        if (srcFunctoid != null && srcFunctoid.Definition?.Name == "Looping") {
                            rawPath = GetLoopingFunctoidRawPath(srcFunctoid.Id);
                        } else {
                            var firstInputWire = _connections.FirstOrDefault(c => c.Target?.FunctoidInstanceId == srcFunctoid?.Id && c.Source?.NodePath != null);
                            if (firstInputWire?.Source != null) {
                                rawPath = $"//{ToValidXPath(firstInputWire.Source.NodePath)}";
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(rawPath)) {
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

            foreach (var child in rootNode.Children.Values) {
                RenderNodeRecursive(child, sb, 2, null);
            }

            sb.AppendLine("  </xsl:template>");
            sb.AppendLine("</xsl:stylesheet>");

            return sb.ToString();
        }

        private string GetLoopingFunctoidRawPath(Guid functoidId) {
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

        private void RenderNodeRecursive(XsltTreeNode node, StringBuilder sb, int indentLevel, string activeLoopPath = null) {
            string indent = new string(' ', indentLevel * 2);

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
            public string? LoopSourcePath { get; set; }
        }

        private string TraceSourceEndpoint(ConnectionEndpoint source, out bool isFunctoidCall, string activeQualifier = null) {
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

                // Automatically detect if a constant/qualifier is wired into this functoid
                string derivedQualifier = activeQualifier;
                if (string.IsNullOrEmpty(derivedQualifier)) {
                    derivedQualifier = DetectQualifierConstant(targetFunctoid.Id, inputConnections);
                }

                var argumentExpressions = new List<string>();
                int totalExpectedSlots = targetFunctoid.Definition?.InputParametersCount ?? 0;

                if (totalExpectedSlots == 0 && inputConnections.Count > 0) {
                    totalExpectedSlots = inputConnections.Max(c => c.Target.InputIndex) + 1;
                }

                for (int slotIndex = 0; slotIndex < totalExpectedSlots; slotIndex++) {
                    var matchingWire = inputConnections.FirstOrDefault(c => c.Target.InputIndex == slotIndex);

                    if (matchingWire != null && matchingWire.Source != null) {
                        if (matchingWire.Source.Type == ConnectionEndpointType.SourceNode) {
                            string cleanPath = ToValidXPath(matchingWire.Source.NodePath, derivedQualifier);
                            argumentExpressions.Add($"string(//{cleanPath})");
                        } else if (matchingWire.Source.Type == ConnectionEndpointType.Functoid) {
                            string nestedCall = TraceSourceEndpoint(matchingWire.Source, out bool innerIsFunctoid, derivedQualifier);
                            argumentExpressions.Add(innerIsFunctoid || nestedCall.StartsWith("user:")
                                ? nestedCall
                                : $"string({nestedCall})");
                        }
                    } else {
                        if (_instanceFallbackParameterCache.TryGetValue(targetFunctoid.Id, out var customDefaults) &&
                            customDefaults.TryGetValue(slotIndex, out string? assignedDefaultLiteral)) {
                            argumentExpressions.Add(assignedDefaultLiteral);
                        } else if (targetFunctoid.CustomMethodName == "StringLeft" && slotIndex == 1) {
                            argumentExpressions.Add("5");
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
                string cleanPath = ToValidXPath(source.NodePath, activeQualifier);
                return $"//{cleanPath}";
            }

            return string.Empty;
        }

        /// <summary>
        /// Scans incoming functoid wires to detect if a String Constant or literal value representing a qualifier (e.g. ST, BY, VN) is attached.
        /// </summary>
        private string? DetectQualifierConstant(Guid functoidId, List<MappingConnection> inputConnections) {
            foreach (var conn in inputConnections) {
                if (conn.Source?.Type == ConnectionEndpointType.Functoid && conn.Source.FunctoidInstanceId.HasValue) {
                    var srcFunctoid = _activeFunctoids.FirstOrDefault(f => f.Id == conn.Source.FunctoidInstanceId.Value);
                    if (srcFunctoid != null && (srcFunctoid.Definition?.Name == "String Constant" || srcFunctoid.CustomMethodName != null)) {
                        string body = srcFunctoid.CustomScriptBody ?? "";
                        if (body.Contains("return")) {
                            int start = body.IndexOf('"');
                            int end = body.LastIndexOf('"');
                            if (start >= 0 && end > start) {
                                return body.Substring(start + 1, end - start - 1);
                            }
                        }
                        if (!string.IsNullOrEmpty(srcFunctoid.CustomMethodName) && srcFunctoid.CustomMethodName.Length <= 4) {
                            return srcFunctoid.CustomMethodName;
                        }
                    }
                }
            }
            return null;
        }

        private string ToValidXPath(string rawPath, string? qualifier = null) {
            if (string.IsNullOrEmpty(rawPath)) return string.Empty;

            string xpath = rawPath;
            if (xpath.Contains("Loop [") && xpath.EndsWith("]")) {
                int startIdx = xpath.IndexOf("[") + 1;
                int len = xpath.Length - 1 - startIdx;
                string loopId = xpath.Substring(startIdx, len).Trim();
                string basePart = xpath.Substring(0, xpath.IndexOf("Loop [")).TrimEnd('/');
                xpath = $"{basePart}/Loop[@LoopId='{loopId}']";
            } else {
                var segments = xpath.Split('/');
                for (int i = 0; i < segments.Length; i++) {
                    if (segments[i].Contains(" [") && segments[i].EndsWith("]")) {
                        segments[i] = segments[i].Replace(" [", "[@LoopId='").Replace("]", "']");
                    } else {
                        segments[i] = segments[i].Replace(" ", "_");
                    }
                }
                xpath = string.Join("/", segments);
            }

            // Automatically inject the N101 qualifier predicate if an active qualifier is detected
            if (!string.IsNullOrEmpty(qualifier) && xpath.Contains("Loop[@LoopId='N1']") && !xpath.Contains("N101=")) {
                xpath = xpath.Replace("Loop[@LoopId='N1']", $"Loop[@LoopId='N1'][N1/N101='{qualifier}']");
            }

            return xpath;
        }
    }
}