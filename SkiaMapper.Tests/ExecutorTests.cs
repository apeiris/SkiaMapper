using Xunit;
using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Threading.Tasks;
using SkiaMapper.Models;
using SkiaMapper.Roslyn;
using System.Xml.Linq;

namespace SkiaMapper.Tests {
    public static class XmlTestDataReader {
        // Flattens an XML file into a path-value dictionary matching your schema tree mapping keys
        public static Dictionary<string, string> FlattenXmlFile(string filePath, string namespacePrefix = "s0") {
            var result = new Dictionary<string, string>();
            if (!System.IO.File.Exists(filePath)) return result;

            var doc = XDocument.Load(filePath);
            if (doc.Root == null) return result;

            // Traverse the tree recursively to build paths
            FlattenElement(doc.Root, namespacePrefix + ":" + doc.Root.Name.LocalName, result);
            return result;
        }

        private static void FlattenElement(XElement element, string currentPath, Dictionary<string, string> result) {
            if (!element.HasElements) {
                result[currentPath] = element.Value;
                return;
            }

            foreach (var child in element.Elements()) {
                string childPath = $"{currentPath}/{child.Name.LocalName}";
                FlattenElement(child, childPath, result);
            }
        }
    }
    public class ExecutorTests {
        [Fact]
        public async Task ExecuteTransformation_WithActualProjectFiles_EvaluatesSuccessfully() {
            // --- 1. Arrange: Identify paths within the output compilation directory ---
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Your canvas template state file (adjust filename to 'yy.xml' or 'canvas.xml' as needed)
            string canvasXmlPath = Path.Combine(baseDir, "yy.xml");
            string sourceXmlPath = Path.Combine(baseDir, "SourceProfile.xml");

            // Verify test files exist in output before executing
            Assert.True(File.Exists(canvasXmlPath), $"Canvas workspace state file missing at: {canvasXmlPath}");
            Assert.True(File.Exists(sourceXmlPath), $"Source XML data profile missing at: {sourceXmlPath}");

            // Load the actual Canvas Connection & Functoid layout state via XML Deserialization
            MappingProjectState projectState;
            var serializer = new XmlSerializer(typeof(MappingProjectState));

            // Using StreamReader instead of naked FileStream safely abstracts encoding disparities and missing BOMs
            using (var fileStream = new FileStream(canvasXmlPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(fileStream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true)) {
                projectState = (MappingProjectState)serializer.Deserialize(reader)!;
            }

            // Extract values directly from your real source XML layout file
            Dictionary<string, string> actualSourceXmlData = XmlTestDataReader.FlattenXmlFile(sourceXmlPath, "s0");

            var executor = new RoslynMapExecutor();

            // --- 2. Act: Run the engine computation pass ---
            var result = await executor.ExecuteTransformationAsync(projectState, actualSourceXmlData);

            // --- 3. Assert: Verify map transformations generated output records ---
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "The transformation engine completed but returned 0 output mappings.");

            // Target key check matching your target pane schema path
            string expectedTargetNode = "ns0:DestinationUser/FullName";
            if (result.TryGetValue(expectedTargetNode, out string evaluatedValue)) {
                System.Diagnostics.Debug.WriteLine($"[TEST LOG] {expectedTargetNode} evaluated to: {evaluatedValue}");
                Assert.False(string.IsNullOrWhiteSpace(evaluatedValue), "Target element evaluated to an empty string.");
            }
        }
    }
}