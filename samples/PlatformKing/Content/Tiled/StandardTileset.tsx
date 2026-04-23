<?xml version="1.0" encoding="UTF-8"?>
<tileset version="1.10" tiledversion="1.11.0" name="TiledIcons" tilewidth="16" tileheight="16" tilecount="1024" columns="32">
 <image source="StandardTilesetIcons.png" width="512" height="512"/>
 <tile id="0" type="SolidCollision"/>
 <tile id="1" type="SolidCollision"/>
 <tile id="2" type="SolidCollision"/>
 <tile id="3" type="JumpThroughCollision"/>
 <tile id="4" type="JumpThroughCollision"/>
 <tile id="5" type="JumpThroughCollision"/>
 <tile id="6" type="OneWayCollision"/>
 <tile id="8" type="ConveyorBelt"/>
 <tile id="9" type="MovingPlatform"/>
 <tile id="11" type="SolidCollision">
  <objectgroup draworder="index" id="2">
   <object id="4" x="0" y="0">
    <polygon points="0,0 16,16 0,16"/>
   </object>
  </objectgroup>
 </tile>
 <tile id="12" type="SolidCollision">
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0">
    <polygon points="0,0 16,8 16,16 0,16"/>
   </object>
  </objectgroup>
 </tile>
 <tile id="13" type="SolidCollision">
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="8">
    <polygon points="0,0 16,8 0,8"/>
   </object>
  </objectgroup>
 </tile>
 <tile id="28" type="Coin"/>
 <tile id="29" type="PlayerSpawn"/>
 <tile id="32" type="Water"/>
 <tile id="33" type="BreakableCollision"/>
 <tile id="34" type="IceCollision"/>
 <tile id="35" type="Wind"/>
 <tile id="36" type="Lava"/>
 <tile id="38" type="Life"/>
 <tile id="39" type="Heal"/>
 <tile id="40" type="FallingPlatform"/>
 <tile id="42" type="Fence"/>
 <tile id="43" type="Navigation"/>
 <tile id="64" type="Door"/>
 <tile id="65" type="PlayerFlag"/>
 <tile id="66" type="EnemyFlag"/>
 <tile id="67" type="Boss"/>
 <tile id="68" type="Goal"/>
 <tile id="69" type="Death"/>
 <tile id="70" type="DamageBlock"/>
 <tile id="71" type="DamageGround"/>
 <tile id="72" type="Slime"/>
 <tile id="73" type="SolidCollision"/>
 <tile id="74" type="SolidCollision"/>
 <tile id="75" type="SolidCollision"/>
 <tile id="76" type="SolidCollision">
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="8" width="16" height="8"/>
  </objectgroup>
 </tile>
 <tile id="77" type="SolidCollision"/>
 <tile id="96" type="Ladder"/>
 <tile id="97" type="Coin"/>
 <tile id="98" type="Key"/>
 <tile id="99" type="LightBulb"/>
 <tile id="100" type="Switch"/>
 <tile id="105" type="SolidCollision"/>
 <tile id="106" type="SolidCollision"/>
 <tile id="107" type="SolidCollision">
  <objectgroup draworder="index" id="2">
   <object id="2" x="8" y="0" width="8" height="16"/>
  </objectgroup>
 </tile>
 <tile id="108" type="SolidCollision"/>
 <tile id="109" type="SolidCollision">
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="8" height="16"/>
  </objectgroup>
 </tile>
 <tile id="139" type="SolidCollision"/>
 <tile id="140" type="SolidCollision">
  <objectgroup draworder="index" id="2">
   <object id="1" x="0" y="0" width="16" height="8"/>
  </objectgroup>
 </tile>
 <tile id="141" type="SolidCollision"/>
 <tile id="256">
  <properties>
   <property name="MatchType" value="Empty"/>
  </properties>
 </tile>
 <tile id="257">
  <properties>
   <property name="MatchType" value="Ignore"/>
  </properties>
 </tile>
 <tile id="258">
  <properties>
   <property name="MatchType" value="NonEmpty"/>
  </properties>
 </tile>
 <tile id="259">
  <properties>
   <property name="MatchType" value="Other"/>
  </properties>
 </tile>
 <tile id="260">
  <properties>
   <property name="MatchType" value="Negate"/>
  </properties>
 </tile>
 <wangsets>
  <wangset name="CollisionSet" type="mixed" tile="-1">
   <wangcolor name="SolidCollision" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="0" wangid="1,1,1,1,1,1,1,1"/>
   <wangtile tileid="73" wangid="1,1,0,0,0,1,1,1"/>
   <wangtile tileid="74" wangid="1,1,1,1,0,0,0,1"/>
   <wangtile tileid="75" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="76" wangid="0,0,0,1,1,1,0,0"/>
   <wangtile tileid="77" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="105" wangid="0,0,0,1,1,1,1,1"/>
   <wangtile tileid="106" wangid="0,1,1,1,1,1,0,0"/>
   <wangtile tileid="107" wangid="0,1,1,1,0,0,0,0"/>
   <wangtile tileid="109" wangid="0,0,0,0,0,1,1,1"/>
   <wangtile tileid="139" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="140" wangid="1,1,0,0,0,0,0,1"/>
   <wangtile tileid="141" wangid="0,0,0,0,0,0,0,1"/>
  </wangset>
 </wangsets>
</tileset>
