<?xml version="1.0" encoding="utf-8"?>
<MappingProject xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" Version="1.0">
  <SourceSchemaPath>C:\EDI\X12\edifactFormatter\850edifactFormatter.xml</SourceSchemaPath>
  <DestinationSchemaPath>D:\REPOS\apeiris\SkiaMapper\bin\Debug\net10.0-windows\outputSchema.xml</DestinationSchemaPath>
  <ActiveFunctoids>
    <FunctoidInstance>
      <Id>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</Id>
      <X>260</X>
      <Y>187</Y>
      <Width>300</Width>
      <Height>150</Height>
      <Definition Id="810" name="CreateNode" catId="8" isVariable="false">
        <MethodNameFormat>CreateNode({0},{1})</MethodNameFormat>
        <JoinOperator />
        <ScriptTemplate>
     public string CreateNode(string N101, string N102, string N103, string N104,
                         string N301,
                         string N401, string N402, string N403, string N404,
                         string targetType) 
{
    // Clean and validate node name to prevent stray quotes
    string rawCode = N101?.Trim().ToUpper() ?? string.Empty;
    string nodeName = rawCode switch {
        "ST" =&gt; "ShipTo",
        "BT" or "BY" =&gt; "Billto",
        "VT" =&gt; "Vendor",
        _ =&gt; null
    };
    
    if (string.IsNullOrEmpty(nodeName) || !nodeName.Equals(targetType, StringComparison.OrdinalIgnoreCase)) 
        return string.Empty;

    string FormatElement(string tag, string val) =&gt;
        string.IsNullOrWhiteSpace(val) ? string.Empty : $"&lt;{tag}&gt;{System.Net.WebUtility.HtmlEncode(val.Trim())}&lt;/{tag}&gt;";

    // Clean string interpolation without extra quotes
    return $"&lt;{nodeName}&gt;" +
        FormatElement("IDCode", N101) +
        FormatElement("Name", N102) +
        FormatElement("GLN", N103) +
        FormatElement("GLNCode", N104) +
        FormatElement("Address1", N301) +
        FormatElement("City", N401) +
        FormatElement("StateProvince", N402) +
        FormatElement("PostalCode", N403) +
        FormatElement("CountryCode", N404) +
        $"&lt;/{nodeName}&gt;";
}
      </ScriptTemplate>
        <InputParametersCount>10</InputParametersCount>
      </Definition>
      <CustomScriptBody>public string CreateNode(string N101, string N102, string N103, string N104,
                         string N301,
                         string N401, string N402, string N403, string N404,
                         string targetType) 
{
    // Clean and validate node name to prevent stray quotes
    string rawCode = N101?.Trim().ToUpper() ?? string.Empty;
    string nodeName = rawCode switch {
        "ST" =&gt; "ShipTo",
        "BT" or "BY" =&gt; "Billto",
        "VT" =&gt; "Vendor",
        _ =&gt; null
    };
    
    if (string.IsNullOrEmpty(nodeName) || !nodeName.Equals(targetType, StringComparison.OrdinalIgnoreCase)) 
        return string.Empty;

    string FormatElement(string tag, string val) =&gt;
        string.IsNullOrWhiteSpace(val) ? string.Empty : $"&lt;{tag}&gt;{System.Net.WebUtility.HtmlEncode(val.Trim())}&lt;/{tag}&gt;";

    // Clean string interpolation without extra quotes
    return $"&lt;{nodeName}&gt;" +
        FormatElement("IDCode", N101) +
        FormatElement("Name", N102) +
        FormatElement("GLN", N103) +
        FormatElement("GLNCode", N104) +
        FormatElement("Address1", N301) +
        FormatElement("City", N401) +
        FormatElement("StateProvince", N402) +
        FormatElement("PostalCode", N403) +
        FormatElement("CountryCode", N404) +
        $"&lt;/{nodeName}&gt;";
}</CustomScriptBody>
      <CustomMethodName>CreateNode</CustomMethodName>
    </FunctoidInstance>
  </ActiveFunctoids>
  <Connections />
</MappingProject>