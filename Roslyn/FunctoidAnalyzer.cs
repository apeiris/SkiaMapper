using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class FunctoidAnalyzer {
    public static FunctoidConstraints AnalyzeTemplate(string scriptTemplate) {
        if (string.IsNullOrWhiteSpace(scriptTemplate)) {
            return new FunctoidConstraints { InitialSlots = 1, IsVariable = false };
        }

        try {
            // CRITICAL FIX: Standalone methods are invalid C# Compilation Units.
            // We must wrap your template string inside a dummy class scope so Roslyn can see it!
            string wrappedCode = $@"
            using System;
            public class DynamicFunctoidContainer {{
                {scriptTemplate}
            }}";

            SyntaxTree tree = CSharpSyntaxTree.ParseText(wrappedCode);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            // Now descendant nodes will successfully resolve the method declaration!
            var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method != null) {
                var parameters = method.ParameterList.Parameters;
                int count = parameters.Count;

                if (count == 0) {
                    return new FunctoidConstraints { InitialSlots = 0, IsVariable = false };
                }

                // Detect the 'params' keyword array modifier
                var lastParam = parameters.Last();
                bool isVariable = lastParam.Modifiers.Any(m => m.IsKind(SyntaxKind.ParamsKeyword));

                if (isVariable) {
                    return new FunctoidConstraints { InitialSlots = 2, IsVariable = true };
                }

                return new FunctoidConstraints { InitialSlots = count, IsVariable = false };
            }
        } catch {
            // Defensive fallback
        }

        return new FunctoidConstraints { InitialSlots = 1, IsVariable = false };
    }
}

public class FunctoidConstraints {
    public int InitialSlots { get; set; } = 1;
    public bool IsVariable { get; set; } = false;
}