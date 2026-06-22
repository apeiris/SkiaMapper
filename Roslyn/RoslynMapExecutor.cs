using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using SkiaMapper.Models;

namespace SkiaMapper.Roslyn {
    public class RoslynMapExecutor {
        public async Task<Dictionary<string, string>> ExecuteTransformationAsync(
            MappingProjectState projectState,
            Dictionary<string, string> sourceXmlValues) {
            var outputs = new Dictionary<string, string>();
            var evaluatedFunctoids = new Dictionary<Guid, string>();

            // 1. Topographical Sort: Order functoids so dependencies resolve first
            var executionOrder = TopographicalSortFunctoids(projectState.ActiveFunctoids, projectState.Connections);

            // 2. Process each functoid sequentially
            foreach (var functoid in executionOrder) {
                List<string> arguments = GatherFunctoidInputs(functoid, projectState.Connections, sourceXmlValues, evaluatedFunctoids);
                string dynamicScript = BuildRoslynExecutionWrapper(functoid, arguments);
                string result = await EvaluateScriptAsync(dynamicScript);

                evaluatedFunctoids[functoid.Id] = result;
            }

            // 3. Map outputs to destination schema nodes
            var targetWires = projectState.Connections.Where(c => c.Target?.Type == ConnectionEndpointType.DestinationNode);
            foreach (var wire in targetWires) {
                string finalValue = string.Empty;

                if (wire.Source.Type == ConnectionEndpointType.SourceNode) {
                    sourceXmlValues.TryGetValue(wire.Source.NodePath, out finalValue);
                } else if (wire.Source.Type == ConnectionEndpointType.Functoid && wire.Source.FunctoidInstanceId.HasValue) {
                    evaluatedFunctoids.TryGetValue(wire.Source.FunctoidInstanceId.Value, out finalValue);
                }

                if (wire.Target != null && !string.IsNullOrEmpty(wire.Target.NodePath)) {
                    outputs[wire.Target.NodePath] = finalValue ?? string.Empty;
                }
            }

            return outputs;
        }

        private string BuildRoslynExecutionWrapper(FunctoidInstance functoid, List<string> arguments) {
            string formattedArgs = string.Join(", ", arguments.Select(a => $"\"{a.Replace("\"", "\\\"")}\""));

            if (functoid.Definition != null && functoid.Definition.IsVariable) {
                return $@"
                    using System;
                    using System.Collections.Generic;
                    using System.Linq;

                    {functoid.CustomScriptBody}
                    
                    return {functoid.CustomMethodName}(new string[] {{ {formattedArgs} }});
                ";
            } else {
                return $@"
                    using System;

                    {functoid.CustomScriptBody}
                    
                    return {functoid.CustomMethodName}({formattedArgs});
                ";
            }
        }

        private async Task<string> EvaluateScriptAsync(string scriptBody) {
            var options = ScriptOptions.Default
                .WithReferences(typeof(object).Assembly, typeof(System.Linq.Enumerable).Assembly);

            var state = await CSharpScript.RunAsync<object>(scriptBody, options);
            return state.ReturnValue?.ToString() ?? string.Empty;
        }

        // --- FIXED: ADDED THE MISSING GATHER METHOD ---
        private List<string> GatherFunctoidInputs(
            FunctoidInstance target,
            IEnumerable<MappingConnection> connections,
            Dictionary<string, string> sourceValues,
            Dictionary<Guid, string> calculatedValues) {
            var boundWires = connections
                .Where(c => c.Target != null && c.Target.Type == ConnectionEndpointType.Functoid && c.Target.FunctoidInstanceId == target.Id)
                .OrderBy(c => c.Target.InputIndex)
                .ToList();

            var resolvedArgs = new List<string>();

            foreach (var wire in boundWires) {
                if (wire.Source.Type == ConnectionEndpointType.SourceNode) {
                    sourceValues.TryGetValue(wire.Source.NodePath, out var val);
                    resolvedArgs.Add(val ?? string.Empty);
                } else if (wire.Source.Type == ConnectionEndpointType.Functoid && wire.Source.FunctoidInstanceId.HasValue) {
                    calculatedValues.TryGetValue(wire.Source.FunctoidInstanceId.Value, out var val);
                    resolvedArgs.Add(val ?? string.Empty);
                }
            }

            return resolvedArgs;
        }

        // --- FIXED: ADDED THE MISSING SORT METHOD ---
        private List<FunctoidInstance> TopographicalSortFunctoids(IEnumerable<FunctoidInstance> functoids, IEnumerable<MappingConnection> connections) {
            var sorted = new List<FunctoidInstance>();
            var visited = new HashSet<Guid>();

            void Visit(FunctoidInstance f) {
                if (visited.Contains(f.Id)) return;
                visited.Add(f.Id);

                var predecessors = connections
                    .Where(c => c.Target?.FunctoidInstanceId == f.Id && c.Source?.Type == ConnectionEndpointType.Functoid && c.Source.FunctoidInstanceId.HasValue)
                    .Select(c => functoids.FirstOrDefault(x => x.Id == c.Source.FunctoidInstanceId.Value))
                    .Where(x => x != null);

                foreach (var pred in predecessors) {
                    Visit(pred!);
                }

                sorted.Add(f);
            }

            foreach (var item in functoids) {
                Visit(item);
            }

            return sorted;
        }
    }
}