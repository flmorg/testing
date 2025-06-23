#!/bin/bash

# macOS postinstall script for Cleanuparr
# This script runs after the PKG installer completes

# Function to log messages
log_message() {
    echo "$(date '+%Y-%m-%d %H:%M:%S') - Cleanuparr Installer: $1"
}

log_message "Starting postinstall script"

# Get the current user (the one who ran the installer)
CURRENT_USER=$(stat -f%Su /dev/console)
USER_HOME=$(eval echo ~$CURRENT_USER)

log_message "Installing for user: $CURRENT_USER"
log_message "User home directory: $USER_HOME"

# Create config directory in user's Application Support
CONFIG_DIR="$USER_HOME/Library/Application Support/Cleanuparr"
log_message "Creating config directory: $CONFIG_DIR"

# Create the directory structure
mkdir -p "$CONFIG_DIR"
mkdir -p "$CONFIG_DIR/logs"

# Copy sample configuration if it doesn't exist
SAMPLE_CONFIG="/Applications/Cleanuparr.app/Contents/Resources/appsettings.json"
USER_CONFIG="$CONFIG_DIR/appsettings.json"

if [ -f "$SAMPLE_CONFIG" ] && [ ! -f "$USER_CONFIG" ]; then
    log_message "Copying sample configuration to user config directory"
    cp "$SAMPLE_CONFIG" "$USER_CONFIG"
else
    log_message "User configuration already exists or sample not found"
fi

# Create a basic configuration if neither exists
if [ ! -f "$USER_CONFIG" ]; then
    log_message "Creating basic configuration file"
    cat > "$USER_CONFIG" << EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "HTTP_PORTS": "11011"
}
EOF
fi

# Set proper ownership and permissions
log_message "Setting ownership and permissions"
chown -R "$CURRENT_USER:staff" "$CONFIG_DIR"
chmod -R 755 "$CONFIG_DIR"
chmod 644 "$USER_CONFIG"

# Create a launch script for easier access
LAUNCH_SCRIPT="$USER_HOME/Desktop/Start Cleanuparr.command"
log_message "Creating desktop launch script: $LAUNCH_SCRIPT"

cat > "$LAUNCH_SCRIPT" << EOF
#!/bin/bash
# Cleanuparr Launch Script
cd "$CONFIG_DIR"
/Applications/Cleanuparr.app/Contents/MacOS/cleanuparr
EOF

chmod +x "$LAUNCH_SCRIPT"
chown "$CURRENT_USER:staff" "$LAUNCH_SCRIPT"

# Create a simple README
README_FILE="$CONFIG_DIR/README.txt"
log_message "Creating README file: $README_FILE"

cat > "$README_FILE" << EOF
Cleanuparr Configuration Directory
==================================

This directory contains your Cleanuparr configuration and logs.

Files:
- appsettings.json: Main configuration file
- logs/: Directory for log files (if file logging is enabled)

To start Cleanuparr:
1. Open Terminal
2. Run: /Applications/Cleanuparr.app/Contents/MacOS/cleanuparr
3. Or double-click the "Start Cleanuparr.command" file on your Desktop

The web interface will be available at: http://localhost:11011

Configuration:
You can modify the appsettings.json file to change settings like:
- Port number (HTTP_PORTS)
- Logging levels
- Other application settings

For more information, visit: https://github.com/flmorg/cleanuperr
EOF

chown "$CURRENT_USER:staff" "$README_FILE"
chmod 644 "$README_FILE"

log_message "Postinstall script completed successfully"

# Optional: Open the config directory in Finder
if [ -x "/usr/bin/open" ]; then
    log_message "Opening configuration directory in Finder"
    sudo -u "$CURRENT_USER" /usr/bin/open "$CONFIG_DIR"
fi

exit 0 