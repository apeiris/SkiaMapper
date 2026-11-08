using System.IO;
using SkiaMapper.Models;

namespace SkiaMapper.Adapters {
    public class XmlSourcePlugin : ISourceDocumentPlugin {
        public string FormatName => "XML Schema / EDI Document";
        public string FileFilter => "XML & EDI Files (*.xml;*.edi;*.txt)|*.xml;*.edi;*.txt|All Files (*.*)|*.*";

        public bool CanHandle(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return false;
            string ext = Path.GetExtension(filePath).ToLower();
            return ext is ".xml" || ext is ".edi" || ext is ".txt";
        }

        public SchemaNode ParseSchema(string filePath) {
            return MainForm.LoadSchemaFromFile(filePath);
        }
    }
}