#!/bin/bash

# LazyInject Build Script
# Builds the Source Generator DLL and copies it to the Plugins folder

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOURCE_GEN_DIR="$SCRIPT_DIR/SourceGenerator"
PLUGINS_DIR="$SCRIPT_DIR/Plugins"
DLL_NAME="Inject.CodeGen.dll"

echo "🔨 Building LazyInject Source Generator..."
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ Error: .NET SDK is not installed."
    echo "   Please install it from https://dotnet.microsoft.com/download"
    exit 1
fi

# Navigate to SourceGenerator directory
cd "$SOURCE_GEN_DIR"

# Clean previous build
echo "🧹 Cleaning previous build..."
dotnet clean -c Release --nologo -v q

# Build the project
echo "📦 Building Release configuration..."
dotnet build -c Release --nologo

# Check if build succeeded
BUILD_OUTPUT="$SOURCE_GEN_DIR/bin/Release/netstandard2.0/$DLL_NAME"
if [ ! -f "$BUILD_OUTPUT" ]; then
    echo "❌ Error: Build failed. DLL not found at $BUILD_OUTPUT"
    exit 1
fi

# Create Plugins directory if it doesn't exist
mkdir -p "$PLUGINS_DIR"

# Copy DLL to Plugins folder
echo "📋 Copying DLL to Plugins folder..."
cp "$BUILD_OUTPUT" "$PLUGINS_DIR/"

echo ""
echo "✅ Build completed successfully!"
echo "   Output: $PLUGINS_DIR/$DLL_NAME"
echo ""
echo "📝 Note: If this is your first time building, you may need to create a .meta file"
echo "   for the DLL in Unity, or let Unity generate one automatically."
