using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SkiaMapper.Models {
    [XmlRoot("MappingProject")]
    public class MappingProjectState {
        [XmlAttribute("Version")]
        public string Version { get; set; } = "1.0";

        [XmlArray("ActiveFunctoids")]
        [XmlArrayItem("FunctoidInstance")]
        public List<FunctoidInstance> ActiveFunctoids { get; set; } = new();

        [XmlArray("Connections")]
        [XmlArrayItem("MappingConnection")]
        public List<MappingConnection> Connections { get; set; } = new();
    }
}