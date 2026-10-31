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
    <MappingConnection>
      <Id>7fad6c51-a53f-44ca-bb4b-b73eb00a108d</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [PO1]/PO1</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/lines/line</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>d16f9dee-dbc4-4456-bccb-e5815a90c013</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [PO1]/PO1/PO101</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/lines/line/@id</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>e7aadb88-e233-4df9-890e-a847cfd894c5</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [PO1]/PO1/PO102</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/lines/line/@qty</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>83f61258-de90-4230-ab5a-17e1aecd649e</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [PO1]/PO1/PO103</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/lines/line/@uom</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>135b7f31-e628-43b0-9f0a-585dcf2c722c</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [PO1]/PO1/PO104</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/lines/line/@price</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>0d9b238a-33a4-4568-94c3-16c79354caaf</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [PO1]/Loop [PID]/PID/PID05</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/lines/line/@description</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
    <MappingConnection>
      <Id>a0c0aaad-16d4-4d6e-a93d-d18ecd6d8ebb</Id>
      <Source>
        <Type>SourceNode</Type>
        <NodePath>Interchange/FunctionGroup/Transaction/Loop [PO1]/PO1/PO107</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Source>
      <Target>
        <Type>DestinationNode</Type>
        <NodePath>po/lines/line/@pid</NodePath>
        <FunctoidInstanceId xsi:nil="true" />
        <InputIndex>0</InputIndex>
      </Target>
    </MappingConnection>
  </Connections>
</MappingProject>