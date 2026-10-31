using System.Collections.Generic;
using System.Xml.Serialization;

namespace SkiaMapper.Models {
    [XmlRoot("MappingProject")]
    public class MappingProjectState {
        [XmlAttribute("Version")]
        public string Version { get; set; } = "1.0";

        [XmlElement("SourceSchemaPath")]
        public string SourceSchemaPath { get; set; } = string.Empty;

        [XmlElement("DestinationSchemaPath")]
        public string DestinationSchemaPath { get; set; } = string.Empty;

        [XmlArray("ActiveFunctoids")]
        [XmlArrayItem("FunctoidInstance")]
        public List<FunctoidInstance> ActiveFunctoids { get; set; } = new();

        [XmlArray("Connections")]
        [XmlArrayItem("MappingConnection")]
        public List<MappingConnection> Connections { get; set; } = new();
    }
}