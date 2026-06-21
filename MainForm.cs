using SkiaMapper.Controls;
using SkiaMapper.Models;
using System;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace SkiaMapper {
    public partial class MainForm : Form {
        private TabControl tcMain;
        private TabPage tbpMapper;
        private SkiaMapperControl mapperControl;

        public MainForm() {
            // 1. Run the designer components
            InitializeComponent();

            // 2. Build our dynamic layout hierarchy
            InitializeCustomMapper();
        }

        private void LoadBuiltInFunctoids() {
            try {
                string functoidsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BuiltInFunctoids.xml");

                if (!File.Exists(functoidsPath)) {
                    return;
                }

                XmlSerializer serializer = new XmlSerializer(typeof(FunctoidContainer));
                using (StreamReader reader = new StreamReader(functoidsPath)) {
                    FunctoidContainer? container = (FunctoidContainer?)serializer.Deserialize(reader);

                    if (container != null) {
                        // 1. Clear active canvas preview states to ensure clean viewport instantiation
                        mapperControl.ActiveFunctoids.Clear();

                        // 2. Map structural layout categories directly into the palette panel grouping view
                        mapperControl.FunctoidCategories = container.Categories;

                        // --- ROSLYN METADATA ANALYSIS STEP ---
                        // 3. Inspect every custom code block signature to resolve its input port metrics
                        if (container.Functoids != null) {
                            foreach (var functoidDef in container.Functoids) {
                                if (string.IsNullOrWhiteSpace(functoidDef.ScriptTemplate)) {
                                    functoidDef.InputParametersCount = 1; // Standard defensive fallback
                                    continue;
                                }

                                // Parse the raw CDATA C# block using Roslyn syntax trees
                                var constraints = FunctoidAnalyzer.AnalyzeTemplate(functoidDef.ScriptTemplate);

                                // Enforce the true structural signature parameter count onto the definition
                                functoidDef.InputParametersCount = constraints.InitialSlots;

                                // OPTIONAL EXTENSION: If your definition model exposes a flag for variable lengths (params),
                                // you can bind it directly like this:
                                // functoidDef.IsVariableLengthInput = constraints.IsVariable;
                            }
                        }

                        // 4. Bind the processed, statically typed definitions to your floating tool palette items list
                        mapperControl.AvailableFunctoids = container.Functoids;

                        // 5. Fire an immediate invalidate repaint target pass down to the SkiaSharp control surface
                        mapperControl.Invalidate();
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show($"Error processing dynamic functoids structural catalog components: {ex.Message}",
                                "Functoid Layout Sync Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnLoad(EventArgs e) {
            base.OnLoad(e);

            // 3. Populate and trigger the Skia surface render engine 
            //    ONLY after the Form handle and layout boundaries are fully created.
            LoadMockSchemaFiles();
            LoadBuiltInFunctoids();
        }

        private void InitializeCustomMapper() {
            // Initialize containers with operational system dimensions
            this.Width = 1100;
            this.Height = 750;
            this.Text = "SkiaMapper - C# Enterprise Pipeline Blueprint";

            this.tcMain = new TabControl { Dock = DockStyle.Fill };
            this.tbpMapper = new TabPage { Text = "Data Mapper Workspace" };

            // Build control instance and explicitly dock it to fill the entire tab view space
            mapperControl = new SkiaMapperControl {
                Dock = DockStyle.Fill
            };

            // Composite structural hierarchy
            this.tbpMapper.Controls.Add(mapperControl);
            this.tcMain.Controls.Add(this.tbpMapper);
            this.Controls.Add(this.tcMain);

            // Force layout recalculation engine to bind elements correctly
            this.PerformLayout();
        }

        private void LoadMockSchemaFiles() {
            try {
                string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SourceProfile.xml");
                string destinationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DestinationUser.xml");

                EnsureSpecimenFilesExist(sourcePath, destinationPath);

                // Parse Source XML
                XmlDocument sourceDoc = new XmlDocument();
                sourceDoc.Load(sourcePath);
                if (sourceDoc.DocumentElement != null) {
                    mapperControl.SourceRoot = BuildSchemaTreeFromXml(sourceDoc.DocumentElement);
                }

                // Parse Destination XML
                XmlDocument destDoc = new XmlDocument();
                destDoc.Load(destinationPath);
                if (destDoc.DocumentElement != null) {
                    mapperControl.DestinationRoot = BuildSchemaTreeFromXml(destDoc.DocumentElement);
                }

                // Seed a placeholder functoid definition mapping block
                mapperControl.ActiveFunctoids.Add(new FunctoidInstance {
                    X = 150,
                    Y = 60,
                    Definition = new FunctoidDefinition { Name = "Concatenate", Id = 100 }
                });

                // Force the Skia canvas engine to paint immediately
                mapperControl.Invalidate();
            } catch (Exception ex) {
                MessageBox.Show($"Error deserializing XML structural schemas: {ex.Message}",
                                "Schema Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private SchemaNode BuildSchemaTreeFromXml(XmlElement xmlElement) {
            string nodeDisplayName = string.IsNullOrEmpty(xmlElement.Prefix)
                ? xmlElement.LocalName
                : $"{xmlElement.Prefix}:{xmlElement.LocalName}";

            SchemaNode treeNode = new SchemaNode {
                Name = nodeDisplayName,
                Namespace = xmlElement.NamespaceURI,
                IsAttribute = false,
                IsExpanded = true
            };

            foreach (XmlNode childXml in xmlElement.ChildNodes) {
                if (childXml is XmlElement childElement) {
                    SchemaNode childTreeNode = BuildSchemaTreeFromXml(childElement);
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
    }
}