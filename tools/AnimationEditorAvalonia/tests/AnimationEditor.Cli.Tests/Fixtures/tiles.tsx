<?xml version="1.0" encoding="UTF-8"?>
<tileset version="1.10" tiledversion="1.12.2" name="Hero" tilewidth="16" tileheight="16" tilecount="6" columns="3">
 <properties>
  <property name="stats" type="class" propertytype="Stats">
   <properties>
    <property name="hp" type="int" value="5"/>
   </properties>
  </property>
  <property name="biome" propertytype="Biome" value="Forest"/>
 </properties>
 <image source="sheet.png" width="48" height="32"/>
 <tile id="0" type="Hero">
  <properties>
   <property name="Name" value="Walk"/>
  </properties>
  <animation>
   <frame tileid="0" duration="100"/>
   <frame tileid="1" duration="100"/>
   <frame tileid="2" duration="200"/>
  </animation>
 </tile>
 <tile id="3" type="SolidCollision">
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="16" height="16"/>
  </objectgroup>
 </tile>
 <tile id="4" type="Water"/>
 <tile id="5">
  <animation>
   <frame tileid="4" duration="150"/>
   <frame tileid="5" duration="150"/>
  </animation>
 </tile>
 <wangsets>
  <wangset name="CollisionSet" type="mixed" tile="-1">
   <wangcolor name="SolidCollision" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="3" wangid="1,1,1,1,1,1,1,1"/>
  </wangset>
 </wangsets>
</tileset>
