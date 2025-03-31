#!/bin/bash

set -e  # Зупинити скрипт при першій помилці

PROJECT_PATH="Weather/Community.PowerToys.Run.Plugin.Weather/Community.PowerToys.Run.Plugin.Weather.csproj"
OUT_ROOT="./Weather/Community.PowerToys.Run.Plugin.Weather/bin"

# 1. Побудова для x64
echo "🛠️  Building for x64..."
dotnet publish "$PROJECT_PATH" -c Release -r win-x64 -p:Platform=x64 -p:PlatformTarget=x64

# 2. Побудова для ARM64
echo "🛠️  Building for ARM64..."
dotnet publish "$PROJECT_PATH" -c Release -r win-arm64 -p:Platform=ARM64 -p:PlatformTarget=ARM64

# 3. Архівування
echo "📦 Zipping results..."

# x64
ZIP_X64="./Weather-x64.zip"
PUBLISH_X64="$OUT_ROOT/x64/Release/net9.0-windows10.0.22621.0/win-x64/publish"
zip -r "$ZIP_X64" "$PUBLISH_X64"/*

# ARM64
ZIP_ARM64="./Weather-ARM64.zip"
PUBLISH_ARM64="$OUT_ROOT/ARM64/Release/net9.0-windows10.0.22621.0/win-arm64/publish"
zip -r "$ZIP_ARM64" "$PUBLISH_ARM64"/*

echo "✅ Done! Created:"
echo " - $ZIP_X64"
echo " - $ZIP_ARM64"
