using SkiaMapper.Controls;
using SkiaMapper.Models;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace SkiaMapper {
    public partial class MainForm : Form {

        // Static Path Accessors
        public static string SourcePath { get; private set; } = string.Empty;
        public static string DestinationPath { get; private set; } = string.Empty;
        public static string XsltPath { get; set; } = string.Empty;

        // Repository Fields
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

            // Default fallback path values at runtime
            string defaultSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SourceProfile.xml");
            string defaultDest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DestinationUser.xml");
            string defaultXslt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Transform.xslt");

            if (string.IsNullOrEmpty(RepositorySourcePath)) RepositorySourcePath = defaultSource;
            if (string.IsNullOrEmpty(RepositoryDestinationPath)) RepositoryDestinationPath = defaultDest;
            if (string.IsNullOrEmpty(XsltPath)) XsltPath = defaultXslt;

            EnsureSpecimenFilesExist(RepositorySourcePath, RepositoryDestinationPath);

            // Populate and trigger rendering engine
            LoadMockSchemaFiles();
            LoadBuiltInFunctoids();
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
                    LoadMockSchemaFiles();
                }
            }
        }

        private void LoadMockSchemaFiles() {
            try {
                if (string.IsNullOrEmpty(RepositorySourcePath) || string.IsNullOrEmpty(RepositoryDestinationPath)) {
                    return;
                }

                if (File.Exists(RepositorySourcePath)) {
                    XmlDocument sourceDoc = new XmlDocument();
                    sourceDoc.Load(RepositorySourcePath);
                    if (sourceDoc.DocumentElement != null) {
                        mapperControl.SourceRoot = BuildSchemaTreeFromXml(sourceDoc.DocumentElement);
                    }
                }

                if (File.Exists(RepositoryDestinationPath)) {
                    XmlDocument destDoc = new XmlDocument();
                    destDoc.Load(RepositoryDestinationPath);
                    if (destDoc.DocumentElement != null) {
                        mapperControl.DestinationRoot = BuildSchemaTreeFromXml(destDoc.DocumentElement);
                    }
                }

                if (mapperControl.ActiveFunctoids.Count == 0) {
                    mapperControl.ActiveFunctoids.Add(new FunctoidInstance {
                        X = 150,
                        Y = 60,
                        Definition = new FunctoidDefinition { Name = "Concatenate", Id = 100 }
                    });
                }

                mapperControl.Invalidate();
            } catch (Exception ex) {
                MessageBox.Show($"Error deserializing XML schemas: {ex.Message}",
                                "Schema Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
    }
}