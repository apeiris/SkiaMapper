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
                // Clear state cache before starting compilation path tracking
                _instanceFallbackParameterCache.Clear();

                string xsltOutput = CompileXsltPayload();

                using (var sfd = new SaveFileDialog()) {
                    sfd.Filter = "XSLT Stylesheet (*.xslt)|*.xslt";
                    sfd.Title = "Save Generated Canvas XSLT Map";
                    sfd.FileName = "CanvasMapExport.xslt";
                   
                   
                    if (sfd.ShowDialog() == DialogResult.OK) {
                        File.WriteAllText(sfd.FileName, xsltOutput, Encoding.UTF8);
                        
                        MainForm.XsltPath = sfd.FileName;
                   
                        // MessageBox.Show("Successfully generated XSLT map with accurate graph tracing!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                       // MainForm.ActiveForm.Text = $"XSLT [  {sfd.FileName} ] generated";
                        MainForm.XsltPath = sfd.FileName; 
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show($"XSLT generation failed:\n{ex.Message}", "Pipeline Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public object BuildDynamicExtensionObject() {
            var codeBuilder = new StringBuilder();
            codeBuilder.AppendLine("using System;");
            codeBuilder.AppendLine("using System.Linq;");
            codeBuilder.AppendLine("namespace SkiaMapper.Dynamic {");
            codeBuilder.AppendLine("    public class FunctoidExtension {");

            // Find all active functoids that are actually connected
            var usedFunctoidIds = _connections
                .Where(c => c.Source != null && c.Source.Type == ConnectionEndpointType.Functoid && c.Source.FunctoidInstanceId.HasValue)
                .Select(c => c.Source.FunctoidInstanceId.Value)
                .Distinct()
                .ToList();

            var activeInstances = _activeFunctoids
                .Where(f => usedFunctoidIds.Contains(f.Id))
                .ToList();

            var addedSignatures = new HashSet<string>();

            foreach (var functoid in activeInstances) {
                if (string.IsNullOrWhiteSpace(functoid.CustomScriptBody)) continue;

                string script = functoid.CustomScriptBody.Trim();

                // Avoid adding duplicate method definitions
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

            // Add basic assembly references for compilation
            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
            };

            // Compile into an in-memory assembly
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

            // Load the compiled assembly and instantiate the class
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
            sb.AppendLine("                exclude-result-prefixes=\"user\">");
            sb.AppendLine("  <xsl:output method=\"xml\" indent=\"yes\" encoding=\"utf-8\"/>");
            sb.AppendLine();

            // -------------------------------------------------------------------------
            // BUILD THE TEMPLATE TRANSFORMATION STRUCTURAL TREE
            // -------------------------------------------------------------------------
            sb.AppendLine("  <xsl:template match=\"/\">");
            sb.AppendLine("    <Output>");

            var terminalConnections = _connections
                .Where(c => c.Target != null && c.Target.Type == ConnectionEndpointType.DestinationNode)
                .OrderBy(c => c.Target.NodePath)
                .ToList();

            foreach (var connection in terminalConnections) {
                string targetNodeName = connection.Target.NodePath;
                sb.AppendLine($"      <{targetNodeName}>");

                string nodeExpression = TraceSourceEndpoint(connection.Source, out bool isFunctoidCall);

                if (!string.IsNullOrEmpty(nodeExpression)) {
                    sb.AppendLine($"        <xsl:value-of select=\"{nodeExpression}\" />");
                }

                sb.AppendLine($"      </{targetNodeName}>");
            }

            sb.AppendLine("    </Output>");
            sb.AppendLine("  </xsl:template>");
            sb.AppendLine("</xsl:stylesheet>");

            return sb.ToString();
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
                            argumentExpressions.Add($"string(//{matchingWire.Source.NodePath})");
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
                    : "Concatenate";

                return $"user:{methodName}({joinedArgs})";
            }

            if (source.Type == ConnectionEndpointType.SourceNode && !string.IsNullOrEmpty(source.NodePath)) {
                return $"//{source.NodePath}";
            }

            return string.Empty;
        }

        /// <summary>
        /// Generates a standardized signature string representation of method nodes to guard against block collision duplications.
        /// </summary>
        private static string ExtractBasicMethodSignatureKey(string methodCode) {
            try {
                SyntaxTree tree = CSharpSyntaxTree.ParseText($"public class DiscoveryShell {{ {methodCode} }}");
                var method = tree.GetCompilationUnitRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (method != null) {
                    string name = method.Identifier.Text;
                    string parameterSignature = string.Join(",", method.ParameterList.Parameters.Select(p => p.Type?.ToString()));
                    return $"{name}({parameterSignature})";
                }
            } catch { }
            return methodCode.Trim();
        }
    }
}