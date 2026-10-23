#!/bin/bash
S=/tmp/claude-0/-home-claude/bcd9b250-548b-5c1f-a1cf-646261cd52c9/scratchpad
cd $S/aimsim
FWREF=$(ls -d /usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
refs=""; for f in $FWREF/*.dll; do refs="$refs -r:$f"; done
A=$S/ucompile/proj/Assets
dotnet /usr/lib/dotnet/sdk/8.0.131/Roslyn/bincore/csc.dll -nologo -noconfig -nostdlib $refs -langversion:9.0 -warn:0 -target:exe -out:$S/aimsim/pure.dll ${DEFS} \
  PureStub.cs $A/Scripts/Guns/ReloadMotionRules.cs $A/Editor/NetDev/NetDevSightsAimTest.cs 2>&1 | head -40
SDK=$(ls /usr/lib/dotnet/shared/Microsoft.NETCore.App/ | head -1)
echo "{\"runtimeOptions\":{\"tfm\":\"net8.0\",\"framework\":{\"name\":\"Microsoft.NETCore.App\",\"version\":\"$SDK\"}}}" > $S/aimsim/pure.runtimeconfig.json
