<?xml version="1.0" encoding="utf-8"?>
<MappingProject xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" Version="1.0">
  <SourceSchemaPath>C:\EDI\X12\edifactFormatter\850edifactFormatter.xml</SourceSchemaPath>
  <DestinationSchemaPath>D:\REPOS\apeiris\SkiaMapper\bin\Debug\net10.0-windows\outputSchema.xml</DestinationSchemaPath>
  <ActiveFunctoids>
    <FunctoidInstance>
      <Id>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</Id>
      <X>463</X>
      <Y>61</Y>
      <Width>83.21686</Width>
      <Height>41.60843</Height>
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
      <CustomScriptBody>public string CreateBillTo(string N101, string N102, string N103, string N104,
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
      <CustomMethodName>CreateBillTo</CustomMethodName>
    </FunctoidInstance>
    <FunctoidInstance>
      <Id>d5532374-e088-48eb-9a86-81c211164ca0</Id>
      <X>322</X>
      <Y>136</Y>
      <Width>80</Width>
      <Height>40</Height>
      <Definition Id="130" name="String Constant" catId="1" isVariable="false">
        <MethodNameFormat>"{0}"</MethodNameFormat>
        <JoinOperator />
        <ScriptTemplate>
      public string StringConstant(string constantValue) {
          return constantValue ?? string.Empty;
      }
      </ScriptTemplate>
        <InputParametersCount>0</InputParametersCount>
      </Definition>
      <CustomScriptBody>public string BillTo() {
          return "BillTo";
      }</CustomScriptBody>
      <CustomMethodName>BillTo</CustomMethodName>
    </FunctoidInstance>
    <FunctoidInstance>
      <Id>61c68536-cdbb-49c7-a38b-cd365ff6250a</Id>
      <X>412</X>
      <Y>253</Y>
      <Width>80</Width>
      <Height>40</Height>
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
      <CustomScriptBody>public string CreateShipTo(string N101, string N102, string N103, string N104,
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
      <CustomMethodName>CreateShipTo</CustomMethodName>
    </FunctoidInstance>
    <FunctoidInstance>
      <Id>b3679b7c-ac84-4cf1-89fe-37daf7b5cc92</Id>
      <X>315</X>
      <Y>323</Y>
      <Width>80</Width>
      <Height>40</Height>
      <Definition Id="130" name="String Constant" catId="1" isVariable="false">
        <MethodNameFormat>"{0}"</MethodNameFormat>
        <JoinOperator />
        <ScriptTemplate>
      public string StringConstant(string constantValue) {
          return constantValue ?? string.Empty;
      }
      </ScriptTemplate>
        <InputParametersCount>0</InputParametersCount>
      </Definition>
      <CustomScriptBody>public string ShipTo() {
          return "ShipTo";
      }</CustomScriptBody>
      <CustomMethodName>ShipTo</CustomMethodName>
    </FunctoidInstance>
  </ActiveFunctoids>
  <Connections>
    <MappingConnection>
      <Id>d25c9b15-06b4-4c05-8c7b-d64ab0fa6499</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N1/N101</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</FunctoidInstanceId>
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>ca1c9d3f-5eca-4333-b7b7-031411964d03</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N1/N102</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</FunctoidInstanceId>
        <InputIndex>1</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>1ffefa87-38c0-473e-87bb-1be4ff6af5f4</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N1/N103</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</FunctoidInstanceId>
        <InputIndex>2</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>ea44ff70-fcd7-470c-a659-9d00216ec4e8</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N1/N104</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</FunctoidInstanceId>
        <InputIndex>3</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>7028e368-4990-42cc-bba6-43c4a5bc853f</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N3/N301</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</FunctoidInstanceId>
        <InputIndex>4</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>3d94e723-7947-4701-ac23-1ae1eb98e9c8</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N4/N401</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</FunctoidInstanceId>
        <InputIndex>5</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>cb3e141f-1246-4cc3-93fc-d67fea18d59b</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N4/N402</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</FunctoidInstanceId>
        <InputIndex>6</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>8aa25b79-243e-47dd-b91f-ca9d7204a8ba</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N4/N403</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</FunctoidInstanceId>
        <InputIndex>7</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>aff5d7f7-1869-48e2-9068-757d63757e31</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N4/N404</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</FunctoidInstanceId>
        <InputIndex>8</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>e5e1947d-d927-4a46-be9b-626bc8a96440</Id>
      <Source>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>d5532374-e088-48eb-9a86-81c211164ca0</FunctoidInstanceId>
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</FunctoidInstanceId>
        <InputIndex>9</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>b6e7b4ff-e441-4bf0-9f2c-37510e24a8cc</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N1/N101</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>61c68536-cdbb-49c7-a38b-cd365ff6250a</FunctoidInstanceId>
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>6fcd8a31-b1a2-4145-9470-24e057f9b693</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N1/N102</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>61c68536-cdbb-49c7-a38b-cd365ff6250a</FunctoidInstanceId>
        <InputIndex>1</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>522f5adc-4499-42c7-9ea3-9d3fbcf439bf</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N1/N103</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>61c68536-cdbb-49c7-a38b-cd365ff6250a</FunctoidInstanceId>
        <InputIndex>2</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>bfd7ad8f-5f6f-4728-bad9-9f309f96636a</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N1/N104</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>61c68536-cdbb-49c7-a38b-cd365ff6250a</FunctoidInstanceId>
        <InputIndex>3</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>42d48672-0ea5-43e7-a4d4-bb2555733569</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N3/N301</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>61c68536-cdbb-49c7-a38b-cd365ff6250a</FunctoidInstanceId>
        <InputIndex>4</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>6c1cfc1a-3323-41ec-a51f-cb1a619cd454</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N4/N401</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>61c68536-cdbb-49c7-a38b-cd365ff6250a</FunctoidInstanceId>
        <InputIndex>5</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>a61e2887-d975-4aa7-94e6-9ab6830a976b</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N4/N402</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>61c68536-cdbb-49c7-a38b-cd365ff6250a</FunctoidInstanceId>
        <InputIndex>6</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>46e1105f-1310-4336-9912-3d495dac15c3</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N4/N403</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>61c68536-cdbb-49c7-a38b-cd365ff6250a</FunctoidInstanceId>
        <InputIndex>7</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>77959f27-d71c-4f17-bff6-a6f9c0eaa521</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N4/N404</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>61c68536-cdbb-49c7-a38b-cd365ff6250a</FunctoidInstanceId>
        <InputIndex>8</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>840d332a-087f-4954-9bef-1191ea65c4cb</Id>
      <Source>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>b3679b7c-ac84-4cf1-89fe-37daf7b5cc92</FunctoidInstanceId>
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>61c68536-cdbb-49c7-a38b-cd365ff6250a</FunctoidInstanceId>
        <InputIndex>9</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>2a0ff5a1-7271-4af7-949d-29503ab0d2ae</Id>
      <Source>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>61c68536-cdbb-49c7-a38b-cd365ff6250a</FunctoidInstanceId>
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/Addresses/ShipTo</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>37e3219e-2182-4232-978a-65587a295e37</Id>
      <Source>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>b514bb5f-9a9b-49ef-97e1-7799f38dfc8c</FunctoidInstanceId>
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/Addresses/BillTo</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>aefdaefa-51b6-4df3-aae4-afdff2914a38</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N1</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/Addresses</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
  </Connections>
</MappingProject>