using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SkiaMapper.Models {
    [XmlRoot("MappingProject")]
    public class MappingProjectState {
        [XmlAttribute("Version")]
        public string Version { get; set; } = "1.0";

        // Stores all custom dropped functoids with positions
        [XmlArray("ActiveFunctoids")]
        [XmlArrayItem("FunctoidInstance")]
        public List<FunctoidInstance> ActiveFunctoids { get; set; } = new();

        // Stores all validated connections 
        [XmlArray("Connections")]
        [XmlArrayItem("MappingConnection")]
        public List<MappingConnection> Connections { get; set; } = new();
    }
}