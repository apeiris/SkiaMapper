<?xml version="1.0" encoding="utf-8"?>
<MappingProject xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" Version="1.0">
  <SourceSchemaPath>C:\EDI\X12\edifactFormatter\850edifactFormatter.xml</SourceSchemaPath>
  <DestinationSchemaPath>D:\REPOS\apeiris\SkiaMapper\bin\Debug\net10.0-windows\outputSchema.xml</DestinationSchemaPath>
  <ActiveFunctoids>
    <FunctoidInstance>
      <Id>522be147-2557-483d-bc42-e9fd007b7499</Id>
      <X>308.2077</X>
      <Y>175</Y>
      <Width>217.79231</Width>
      <Height>75.42592</Height>
      <Definition Id="840" name="FilterAddress" catId="8" isVariable="false">
        <MethodNameFormat>FilterAddress({0}, {1}, {2}, {3}, {4}, {5}, {6})</MethodNameFormat>
        <JoinOperator />
        <ScriptTemplate>
      public string FilterAddress(string n101, string n102, string n301, string n401, string n402, string n403, string targetQualifier) {
          if (string.Equals(n101?.Trim(), targetQualifier?.Trim(), StringComparison.OrdinalIgnoreCase)) {
              var parts = new[] { n102, n301, n401, $"{n402} {n403}" }
                          .Where(p =&gt; !string.IsNullOrWhiteSpace(p))
                          .Select(p =&gt; p.Trim());
              return string.Join(", ", parts);
          }
          return string.Empty;
      }
      </ScriptTemplate>
        <InputParametersCount>7</InputParametersCount>
      </Definition>
      <CustomScriptBody>public string FilterAddress(string n101, string n102, string n301, string n401, string n402, string n403, string targetQualifier) {
          if (string.Equals(n101?.Trim(), targetQualifier?.Trim(), StringComparison.OrdinalIgnoreCase)) {
              var parts = new[] { n102, n301, n401, $"{n402} {n403}" }
                          .Where(p =&gt; !string.IsNullOrWhiteSpace(p))
                          .Select(p =&gt; p.Trim());
              return string.Join(", ", parts);
          }
          return string.Empty;
      }</CustomScriptBody>
      <CustomMethodName>FilterAddress</CustomMethodName>
    </FunctoidInstance>
    <FunctoidInstance>
      <Id>2d93257e-ba9d-4bbb-bf18-b41fcc091eb4</Id>
      <X>270</X>
      <Y>322</Y>
      <Width>110</Width>
      <Height>36</Height>
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
      <CustomScriptBody>public string ST() {
          return "ST";
      }</CustomScriptBody>
      <CustomMethodName>ST</CustomMethodName>
    </FunctoidInstance>
  </ActiveFunctoids>
  <Connections>
    <MappingConnection>
      <Id>514b99e8-6c21-44f9-9433-d534013ac640</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N1/N101</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>522be147-2557-483d-bc42-e9fd007b7499</FunctoidInstanceId>
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>129f93ff-2121-4630-9f0e-504210387f77</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N1/N102</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>522be147-2557-483d-bc42-e9fd007b7499</FunctoidInstanceId>
        <InputIndex>1</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>c3ae0c97-84a2-443a-ada0-2de9d33e61b0</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N3/N301</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>522be147-2557-483d-bc42-e9fd007b7499</FunctoidInstanceId>
        <InputIndex>2</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>7e9d2cea-ef48-4128-adff-c7bc2f3940a1</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N4/N401</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>522be147-2557-483d-bc42-e9fd007b7499</FunctoidInstanceId>
        <InputIndex>3</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>91b46b87-e203-4251-a60c-83a4e1c1d9b9</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N4/N402</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>522be147-2557-483d-bc42-e9fd007b7499</FunctoidInstanceId>
        <InputIndex>4</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>ae3f3baa-408f-4fdb-930f-cbcd52c8fabd</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [N1]/N4/N403</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>522be147-2557-483d-bc42-e9fd007b7499</FunctoidInstanceId>
        <InputIndex>5</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>1afe4092-967d-4827-9b14-1403d316e697</Id>
      <Source>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>2d93257e-ba9d-4bbb-bf18-b41fcc091eb4</FunctoidInstanceId>
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>522be147-2557-483d-bc42-e9fd007b7499</FunctoidInstanceId>
        <InputIndex>6</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>ba8c59c9-79cf-4d11-aa96-ddca577cef27</Id>
      <Source>
        <Type>Functoid</Type>
        <NodePath />
        <FunctoidInstanceId>522be147-2557-483d-bc42-e9fd007b7499</FunctoidInstanceId>
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/Addresses/ShipTo</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
  </Connections>
</MappingProject>