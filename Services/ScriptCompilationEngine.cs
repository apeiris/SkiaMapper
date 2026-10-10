using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SkiaMapper.Services {
    public static class ScriptCompilationEngine {
        /// <summary>
        /// Dynamically compiles and executes a functoid's script body using Roslyn,
        /// injecting variable array arguments safely.
        /// </summary>
        public static object? ExecuteFunctoidScript(string scriptBody, string functionName, List<object> inputArguments) {
            // 1. Generate unique class wrapper structure to house the script body safely
            string typeName = $"FunctoidWrapper_{Guid.NewGuid().ToString("N")}";

            // Re-map inputs dynamically to pass to our invocation method array signatures
            string paramSignature = string.Join(", ", inputArguments.Select((arg, index) => $"object param{index}"));
            string innerArgs = string.Join(", ", inputArguments.Select((arg, index) => $"param{index}"));

            string SourceCode = $@"
                using System;
                using System.Text;
                using System.Collections.Generic;
                using System.Linq;

                public static class {typeName}
                {{
                    // The core script asset body from the user workspace / registry
                    {scriptBody}

                    // The dynamic execution proxy pipeline
                    public static object ExecuteProxy({paramSignature})
                    {{
                        return {functionName}({innerArgs});
                    }}
                }}";

            // 2. Setup Syntax Tree and Optimization settings targeting .NET 10 LTS
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(SourceCode);
            string assemblyName = Path.GetRandomFileName();

            // Gather standard diagnostic runtime reference location maps
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("System.Runtime")).Location)
            };

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release)
            );

            // 3. Emit assembly byte payload completely in-memory
            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success) {
                // Format compilation warnings/errors to funnel directly to your RichTextBox UI Logger logs
                var errors = string.Join(Environment.NewLine, result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));

                throw new InvalidOperationException($"Roslyn Script Compilation Failed:{Environment.NewLine}{errors}");
            }

            // 4. Load assembly and invoke the proxy method instantly via reflection handles
            ms.Seek(0, SeekOrigin.Begin);
            Assembly assembly = Assembly.Load(ms.ToArray());
            Type? type = assembly.GetType(typeName);
            MethodInfo? method = type?.GetMethod("ExecuteProxy");

            if (method == null)
                throw new MissingMethodException($"Target execution handle '{functionName}' could not be located in scope.");

            // Invoke and pass down the arguments array objects
            return method.Invoke(null, inputArguments.ToArray());
        }
    }
}