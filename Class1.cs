using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace SkiaMapper {
    internal class Class1 {
        //====================================================
        public string CreateNode(string N101, string N102, string N103, string N104,
                             string N301,
                             string N401, string N402, string N403, string N404) {
            // 1. Resolve node name using the switch expression
            string nodeName = N101?.Trim().ToUpper() switch {
                "ST" => "ShipTo",
                "BT" or "BY" => "BillTo",
                "VT" => "Vendor",
                _ => !string.IsNullOrWhiteSpace(N101) ? N101 : "Unknown"
            };
          
            // 2. Helper to safely format XML elements using HtmlEncode
            string FormatElement(string tag, string val) =>
                string.IsNullOrWhiteSpace(val) ? string.Empty : $"<{tag}>{System.Net.WebUtility.HtmlEncode(val.Trim())}</{tag}>";

            // 3. Construct and return the XML fragment string
            return $@"<{nodeName}>" +
                FormatElement("IDCode", N101) +
                FormatElement("Name", N102) +
                FormatElement("GLN", N103) +
                FormatElement("GLNCode", N104) +
                FormatElement("Address1", N301) +
                FormatElement("City", N401) +
                FormatElement("StateProvince", N402) +
                FormatElement("PostalCode", N403) +
                FormatElement("CountryCode", N404) +
            $"</{nodeName}>";
        }
//===========================================
    }

}