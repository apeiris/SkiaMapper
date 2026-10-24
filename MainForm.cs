using SkiaMapper.Controls;
using SkiaMapper.Models;
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
namespace SkiaMapper {
    public partial class MainForm : Form {
        public static string SourcePath { get; set; } = string.Empty;
        public static string DestinationPath { get; set; } = string.Empty;
        public static string XsltPath { get; set; } = string.Empty;
        private string repositorySourcePath = string.Empty;
        private string repositoryDestinationPath = string.Empty;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string RepositorySourcePath {
            get => repositorySourcePath;
            set {
                repositorySourcePath = value;
                SourcePath = value;
                if (txtRepositorySource != null && txtRepositorySource.Text != value) {
                    txtRepositorySource.Text = value;
                }
            }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string RepositoryDestinationPath {
            get => repositoryDestinationPath;
            set {
                repositoryDestinationPath = value;
                DestinationPath = value;
                if (txtRepositoryDestination != null && txtRepositoryDestination.Text != value) {
                    txtRepositoryDestination.Text = value;
                }
            }
        }
        public MainForm() {
            InitializeComponent();
            WireEvents();
        }
        private void WireEvents() {
            txtRepositorySource.TextChanged += (s, e) => {
                repositorySourcePath = txtRepositorySource.Text;
                SourcePath = txtRepositorySource.Text;
            };

            txtRepositoryDestination.TextChanged += (s, e) => {
                repositoryDestinationPath = txtRepositoryDestination.Text;
                DestinationPath = txtRepositoryDestination.Text;
            };

            txtXsltFile.TextChanged += (s, e) => {
                XsltPath = txtXsltFile.Text;
            };

            btnBrowseSource.Click += (s, e) => BrowseForFile(isSource: true);
            btnBrowseDestination.Click += (s, e) => BrowseForFile(isSource: false);
            btnBrowseXslt.Click += (s, e) => BrowseForFile(isSource: false, ftypes: "XSLT Files (*.xslt)|*.xslt|All Files (*.*)|*.*");
        }
        protected override void OnLoad(EventArgs e) {
            base.OnLoad(e);
            // Guard against running file IO/Roslyn parsing during Visual Studio design mode
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode) {
                return;
            }
            string path = AppDomain.CurrentDomain.BaseDirectory;
            string defaultSource = Path.Combine(path, "850_POMultiline.xml");
            string defaultDest = Path.Combine(path, "outputSchema.xml");
            string defaultXslt = Path.Combine(path, "Transform.xslt");
            if (string.IsNullOrEmpty(RepositorySourcePath)) RepositorySourcePath = defaultSource;
            if (string.IsNullOrEmpty(RepositoryDestinationPath)) RepositoryDestinationPath = defaultDest;
            if (string.IsNullOrEmpty(XsltPath)) XsltPath = defaultXslt;
            EnsureSpecimenFilesExist(RepositorySourcePath, RepositoryDestinationPath);
            LoadBuiltInFunctoids();
            if (File.Exists(defaultSource)) {
                SchemaNode? sroot = LoadSchemaFromFile(defaultSource);
                if (sroot != null) {
                    mapperControl.SourceRoot = sroot;
                }
                SchemaNode? droot = LoadSchemaFromFile(defaultDest);

                if (droot != null) {
                    mapperControl.DestinationRoot = droot;
                }

                mapperControl.Invalidate();
            }
        }
        private void LoadBuiltInFunctoids() {
            try {
                string functoidsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BuiltInFunctoids.xml");

                if (!File.Exists(functoidsPath)) return;

                XmlSerializer serializer = new XmlSerializer(typeof(FunctoidContainer));
                using (StreamReader reader = new StreamReader(functoidsPath)) {
                    FunctoidContainer? container = (FunctoidContainer?)serializer.Deserialize(reader);

                    if (container != null) {
                        mapperControl?.ActiveFunctoids.Clear();
                        if (mapperControl != null) mapperControl.FunctoidCategories = container.Categories;

                        if (container.Functoids != null) {
                            foreach (var functoidDef in container.Functoids) {
                                if (string.IsNullOrWhiteSpace(functoidDef.ScriptTemplate)) {
                                    functoidDef.InputParametersCount = 1;
                                    continue;
                                }

                                var constraints = FunctoidAnalyzer.AnalyzeTemplate(functoidDef.ScriptTemplate);
                                functoidDef.InputParametersCount = constraints.InitialSlots;
                            }
                        }

                        if (mapperControl != null) mapperControl.AvailableFunctoids = container?.Functoids ?? new();
                        mapperControl?.Invalidate();
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show($"Error processing functoids catalog: {ex.Message}",
                                "Functoid Layout Sync Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void BrowseForFile(bool isSource, string ftypes = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*") {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
                ofd.Filter = ftypes;
                ofd.Title = isSource ? "Select Source XML Schema" : "Select File";

                string currentPath = isSource ? RepositorySourcePath : RepositoryDestinationPath;
                if (!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath)) {
                    ofd.InitialDirectory = Path.GetDirectoryName(currentPath);
                    ofd.FileName = Path.GetFileName(currentPath);
                }

                if (ofd.ShowDialog() == DialogResult.OK) {
                    if (isSource) {
                        RepositorySourcePath = ofd.FileName;
                    } else {
                        RepositoryDestinationPath = ofd.FileName;
                    }

                }
            }
        }
        public static SchemaNode? LoadSchemaFromFile(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);

                if (doc.DocumentElement == null) return null;

                // Check if the file is a formal W3C XSD schema vs. an XML instance
                bool isXsdSchema = doc.DocumentElement.LocalName == "schema" ||
                                   doc.DocumentElement.NamespaceURI == "http://www.w3.org/2001/XMLSchema";

                if (isXsdSchema) {
                    return LoadSchemaFromXsd(filePath);
                } else {
                    // Programmatically convert XML instance into SchemaNode tree
                    string xsdFile = Path.ChangeExtension(filePath, ".xsd");
                    SchemaNode xsdNode = BuildSchemaTreeFromXmlStatic(doc.DocumentElement, 0);

                    SchemaNodeToXsdExporter.ExportSchemaNodeToXsd(xsdNode, xsdFile);

                    return xsdNode;
                }
            } catch (Exception ex) {
                ErrorHelper.HandleException(ex, $"Failed to parse schema from file:\n{ex.Message}");
                return null;
            }
        }


   
        private static SchemaNode? LoadSchemaFromXsd(string xsdPath) {
            try {
                using (FileStream fs = new FileStream(xsdPath, FileMode.Open, FileAccess.Read)) {
                    XmlSchema schema = XmlSchema.Read(fs, null);
                    if (schema == null) return null;

                    // CRITICAL: Compile the schema to resolve named types (ISAType, FunctionGroupType, etc.)
                    schema.Compile(null);

                    // Find the root element in the schema items
                    foreach (XmlSchemaObject item in schema.Items) {
                        if (item is XmlSchemaElement element) {
                            return BuildSchemaNodeFromXsdElement(element, 0);
                        }
                    }
                }
            } catch (Exception ex) {
                //  MessageBox.Show($"Failed to parse XSD file:\n{ex.Message}", "XSD Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ErrorHelper.HandleException(ex, "Failed to parse xsd file");

            }
            return null;
        }
        private static SchemaNode BuildSchemaNodeFromXsdElement(XmlSchemaElement element, int currentDepth) {
            string nodeName = element.Name ?? element.QualifiedName.Name;

            SchemaNode treeNode = new SchemaNode {
                Name = nodeName,
                IsAttribute = false,
                IsExpanded = true,
                Depth = currentDepth
            };

            if (element.ElementSchemaType is XmlSchemaComplexType complexType) {
                // Extract attributes
                if (complexType.AttributeUses != null) {
                    foreach (XmlSchemaAttribute attr in complexType.AttributeUses.Values) {
                        string attrName = attr.Name ?? attr.QualifiedName.Name;
                        treeNode.Children.Add(new SchemaNode {
                            Name = $"@{attrName}",
                            IsAttribute = true,
                            IsExpanded = false,
                            Depth = currentDepth + 1
                        });
                    }
                }

                // Extract child elements from content particles (sequences, choices, etc.)
                if (complexType.ContentTypeParticle is XmlSchemaParticle particle) {
                    ProcessXsdParticle(particle, treeNode, currentDepth + 1);
                }
            }

            return treeNode;
        }
        private static void ProcessXsdParticle(XmlSchemaParticle particle, SchemaNode parentNode, int currentDepth) {
            if (particle is XmlSchemaGroupBase groupBase) {
                foreach (XmlSchemaObject item in groupBase.Items) {
                    if (item is XmlSchemaElement childElement) {
                        parentNode.Children.Add(BuildSchemaNodeFromXsdElement(childElement, currentDepth));
                    } else if (item is XmlSchemaParticle nestedParticle) {
                        ProcessXsdParticle(nestedParticle, parentNode, currentDepth);
                    }
                }
            }
        }
        public static SchemaNode BuildSchemaTreeFromXmlStatic(XmlElement element, int depth = 0) {
            // If it's a generic container like <Loop>, append its LoopId for clear visibility in the UI tree
            string displayName = element.Name;
            string loopId = element.GetAttribute("LoopId");
            if (!string.IsNullOrEmpty(loopId)) {
                displayName = $"Loop [{loopId}]";
            }

            SchemaNode node = new SchemaNode {
                Name = displayName,
                IsAttribute = false,
                IsExpanded = true,
                Depth = depth
            };

            // 1. Extract XML Attributes (including LoopId, Name, segment-terminator, etc.)
            foreach (XmlAttribute attr in element.Attributes) {
                node.Children.Add(new SchemaNode {
                    Name = $"@{attr.Name}",
                    IsAttribute = true,
                    IsExpanded = false,
                    Depth = depth + 1
                });
            }

            // 2. Track unique child signatures using both tag name and LoopId
            HashSet<string> processedChildSignatures = new HashSet<string>();

            foreach (XmlNode childNode in element.ChildNodes) {
                if (childNode is XmlElement childElement) {
                    string childName = childElement.Name;
                    string childLoopId = childElement.GetAttribute("LoopId");

                    // Create a unique signature so <Loop LoopId="N1"> and <Loop LoopId="PO1"> are treated as distinct nodes
                    string signature = string.IsNullOrEmpty(childLoopId) ? childName : $"{childName}_{childLoopId}";

                    if (!processedChildSignatures.Contains(signature)) {
                        processedChildSignatures.Add(signature);
                        node.Children.Add(BuildSchemaTreeFromXmlStatic(childElement, depth + 1));
                    }
                }
            }

            return node;
        }
        private SchemaNode BuildSchemaTreeFromXml(XmlElement xmlElement, int currentDepth = 0) {
            string nodeDisplayName = string.IsNullOrEmpty(xmlElement.Prefix)
                ? xmlElement.LocalName
                : $"{xmlElement.Prefix}:{xmlElement.LocalName}";

            SchemaNode treeNode = new SchemaNode {
                Name = nodeDisplayName,
                IsAttribute = false,
                IsExpanded = true,
                Depth = currentDepth
            };

            if (xmlElement.Attributes != null) {
                foreach (XmlAttribute attr in xmlElement.Attributes) {
                    if (attr.Prefix == "xmlns" || attr.LocalName == "xmlns") continue;

                    string attrDisplayName = string.IsNullOrEmpty(attr.Prefix)
                        ? $"@{attr.LocalName}"
                        : $"@{attr.Prefix}:{attr.LocalName}";

                    treeNode.Children.Add(new SchemaNode {
                        Name = attrDisplayName,
                        IsAttribute = true,
                        IsExpanded = false,
                        Depth = currentDepth + 1
                    });
                }
            }

            foreach (XmlNode childXml in xmlElement.ChildNodes) {
                if (childXml is XmlElement childElement) {
                    SchemaNode childTreeNode = BuildSchemaTreeFromXml(childElement, currentDepth + 1);
                    treeNode.Children.Add(childTreeNode);
                }
            }

            return treeNode;
        }
        private void EnsureSpecimenFilesExist(string sourcePath, string destPath) {
            if (!File.Exists(sourcePath)) {
                string rawSourceXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <s0:SourceProfile xmlns:s0=""http://SchemaSample.SourceProfile"">
                        <Id>USR-1042</Id>
                          <FirstName>Jane</FirstName>
                          <LastName>Doe</LastName>
                          <BirthDate>1992-05-15</BirthDate>
                          <PostalCode>90210</PostalCode>
                        </s0:SourceProfile>";
                File.WriteAllText(sourcePath, rawSourceXml);
            }

            if (!File.Exists(destPath)) {
                string rawDestXml = @"<ns0:DestinationUser xmlns:ns0=""http://SchemaSample.DestinationUser"">
                                        <UserId>USR-1042</UserId>
                                        <UserId_Plus_1000></UserId_Plus_1000>
                                        <FullName>Jane Doe</FullName>
                                        <Age>34</Age>
                                        <Zip>90210</Zip>
                                        <Zip_Firstname></Zip_Firstname>
                                        <Age_Plus_10_Days></Age_Plus_10_Days>
                                        <One_Plus_Two></One_Plus_Two>
                                        <Ten_Into_Three></Ten_Into_Three>
                                        </ns0:DestinationUser>";
                File.WriteAllText(destPath, rawDestXml);
            }
        }


        public static class SchemaStorageHelper {
            // Save the SchemaNode tree to a JSON file
            public static void SaveSchemaToFile(SchemaNode rootNode, string filePath) {
                var options = new JsonSerializerOptions {
                    WriteIndented = true, // Makes the saved file human-readable
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string jsonString = JsonSerializer.Serialize(rootNode, options);
                File.WriteAllText(filePath, jsonString);
            }

            // Load the SchemaNode tree back from a JSON file
            public static SchemaNode? LoadSavedSchema(string filePath) {
                if (!File.Exists(filePath)) return null;

                string jsonString = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<SchemaNode>(jsonString, options);
            }
        }
    }
}