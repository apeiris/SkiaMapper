using SkiaMapper.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SkiaMapper.Adapters {
    public interface ISourceDocumentPlugin {
        string FormatName { get; }
        string FileFilter { get; }
        bool CanHandle(string filePath);
        SchemaNode ParseSchema(string filePath);
    }

}
