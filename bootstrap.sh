#!/bin/bash
set -e

if [[ -z "$1" ]]; then
  echo Usage:
  echo 
  echo "$0 <Platform> [NoClean]"
  echo 
  echo "Where <Platform> can be Linux|MacOS|Windows"
  echo
  echo "If NoClean is specified, submodules won't be updated"
  exit -1
fi

HOST=`uname`
if [[ x$HOST != xMINGW* ]]; then
	CMD_PREFIX="mono ./"
else
	CMD_PREFIX="./"
fi
echo Current directory: `pwd`

if [[ x$2 != xNoClean ]]; then
  git submodule init
  git submodule update 
fi

${CMD_PREFIX}Protobuild.exe --generate $1 
sed -i~ -e "s/\(<UseSGen>False<\/UseSGen>\)/\1<Externalconsole>true<\/Externalconsole>/" examples/SmartScopeConsole/SmartScopeConsole.$1.csproj

${CMD_PREFIX}.nuget/NuGet.exe restore DeviceInterface.$1.sln
