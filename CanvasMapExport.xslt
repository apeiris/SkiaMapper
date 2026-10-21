<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" 
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:user="urn:my-scripts"
                exclude-result-prefixes="user">
  <xsl:output method="xml" indent="yes" encoding="utf-8"/>

  <xsl:template match="/">
    <po>
      <billto>
        <a1>
          <xsl:value-of select="//Interchange/ISA/ISA09" />
        </a1>
        <a2>
          <xsl:value-of select="//Interchange/ISA/ISA02" />
        </a2>
        <postalCode>
          <xsl:value-of select="//Interchange/ISA/ISA05" />
        </postalCode>
        <City>
          <xsl:value-of select="//Interchange/ISA/ISA06" />
        </City>
        <Country>
          <xsl:value-of select="//Interchange/ISA/ISA06" />
        </Country>
      </billto>
      <DeliverTo>
        <postalCode>
          <xsl:value-of select="//Interchange/ISA/ISA11" />
        </postalCode>
        <a1>
          <xsl:value-of select="//Interchange/ISA/ISA03" />
        </a1>
      </DeliverTo>
    </po>
  </xsl:template>
</xsl:stylesheet>
