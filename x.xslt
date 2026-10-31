<?xml version="1.0" encoding="utf-8"?>
<MappingProject xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" Version="1.0">
  <SourceSchemaPath>C:\EDI\X12\edifactFormatter\850edifactFormatter.xml</SourceSchemaPath>
  <DestinationSchemaPath>D:\REPOS\apeiris\SkiaMapper\outputSchema.xml</DestinationSchemaPath>
  <ActiveFunctoids />
  <Connections>
    <MappingConnection>
      <Id>327f9995-316e-4fc3-8690-f6a8de1be044</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/ISA/ISA01</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/transaction/@typeId</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>4579ca6c-b173-4a75-9f15-7859c7b9795c</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/ST/ST01</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/transaction/@x12</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
  </Connections>
</MappingProject>