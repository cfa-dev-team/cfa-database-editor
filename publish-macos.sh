#!/bin/bash
set -e

APP_NAME="CFA Database Editor"
BUNDLE_ID="com.uniquekid.cfadatabaseeditor"
EXECUTABLE="CfaDatabaseEditor"
VERSION="1.1.0"

# Detect architecture
ARCH="${1:-$(uname -m)}"
case "$ARCH" in
    arm64|aarch64) RID="osx-arm64" ;;
    x86_64|x64)    RID="osx-x64" ;;
    *)             echo "Usage: $0 [arm64|x64]"; exit 1 ;;
esac

echo "Publishing for $RID..."
dotnet publish CfaDatabaseEditor/CfaDatabaseEditor.csproj -c Release -r "$RID"

PUBLISH_DIR="CfaDatabaseEditor/bin/Release/net8.0/$RID/publish"
APP_BUNDLE="$PUBLISH_DIR/$APP_NAME.app"

# Clean previous bundle
rm -rf "$APP_BUNDLE"

# Create .app structure
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Move executable
mv "$PUBLISH_DIR/$EXECUTABLE" "$APP_BUNDLE/Contents/MacOS/$EXECUTABLE"
chmod +x "$APP_BUNDLE/Contents/MacOS/$EXECUTABLE"

# Create Info.plist
cat > "$APP_BUNDLE/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleExecutable</key>
    <string>$EXECUTABLE</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
</dict>
</plist>
PLIST

echo ""
echo "Built: $APP_BUNDLE"
echo "To install, drag to /Applications or run directly."
