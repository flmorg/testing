# Cleanuparr Installation Guide

This guide covers all installation methods for Cleanuparr across different platforms.

## 📋 System Requirements

### Windows
- **OS**: Windows 10/11 or Windows Server 2019+
- **Architecture**: x64 (64-bit)
- **Memory**: 512MB RAM minimum, 1GB recommended
- **Storage**: 100MB free space

### macOS
- **Intel Macs**: macOS 10.15 Catalina or later
- **Apple Silicon Macs**: macOS 11.0 Big Sur or later
- **Memory**: 512MB RAM minimum, 1GB recommended
- **Storage**: 100MB free space

### Linux
- **Distribution**: Any modern Linux distribution
- **Kernel**: 3.2+ with glibc 2.17+
- **Architecture**: x64 (AMD64) or ARM64
- **Memory**: 512MB RAM minimum, 1GB recommended
- **Storage**: 100MB free space

## 🚀 Installation Methods

### Windows

#### Option 1: Windows Installer (Recommended)
1. Download `Cleanuparr-{version}-Setup.exe` from [Releases](https://github.com/flmorg/cleanuperr/releases)
2. Run the installer as Administrator
3. Follow the installation wizard:
   - Choose installation directory (default: `C:\Program Files\Cleanuparr`)
   - **Optional**: Install as Windows Service (recommended for always-on usage)
   - **Optional**: Create desktop shortcut
4. The installer will automatically:
   - Install Cleanuparr
   - Create configuration directories
   - Start the service (if selected)
   - Open the web interface

#### Option 2: Portable Executable
1. Download `cleanuperr-win-amd64.zip` from [Releases](https://github.com/flmorg/cleanuperr/releases)
2. Extract to your preferred location
3. Review and modify `appsettings.json` if needed
4. Run `cleanuparr.exe`
5. Access the web interface at http://localhost:11011

### macOS

#### Option 1: PKG Installer (Recommended)
1. Download the appropriate installer:
   - **Intel Macs**: `Cleanuparr-{version}-macos-intel.pkg`
   - **Apple Silicon Macs**: `Cleanuparr-{version}-macos-arm64.pkg`
2. Double-click the PKG file to start installation
3. Follow the installation wizard
4. The installer will:
   - Install Cleanuparr to `/Applications/Cleanuparr.app`
   - Create configuration directory at `~/Library/Application Support/Cleanuparr`
   - Create a desktop shortcut for easy launching
   - Open the configuration directory in Finder

#### Option 2: Portable Executable
1. Download the appropriate archive:
   - **Intel Macs**: `cleanuperr-osx-amd64.zip`
   - **Apple Silicon Macs**: `cleanuperr-osx-arm64.zip`
2. Extract to your preferred location
3. Review and modify `appsettings.json` if needed
4. Open Terminal and navigate to the extracted folder
5. Make executable: `chmod +x cleanuparr`
6. Run: `./cleanuparr`
7. Access the web interface at http://localhost:11011

### Linux

#### Portable Executable (Only Option)
1. Download the appropriate archive:
   - **x64 Systems**: `cleanuperr-linux-amd64.zip`
   - **ARM64 Systems**: `cleanuperr-linux-arm64.zip`
2. Extract to your preferred location:
   ```bash
   unzip cleanuperr-linux-*.zip
   cd cleanuperr-linux-*/
   ```
3. Review and modify `appsettings.json` if needed
4. Make executable: `chmod +x cleanuparr`
5. Run: `./cleanuparr`
6. Access the web interface at http://localhost:11011

#### System Service (Optional)
To run Cleanuparr as a system service on Linux:

1. Create a systemd service file:
   ```bash
   sudo nano /etc/systemd/system/cleanuparr.service
   ```

2. Add the following content (adjust paths as needed):
   ```ini
   [Unit]
   Description=Cleanuparr
   After=network.target

   [Service]
   Type=simple
   User=cleanuparr
   WorkingDirectory=/opt/cleanuparr
   ExecStart=/opt/cleanuparr/cleanuparr
   Restart=always
   RestartSec=10

   [Install]
   WantedBy=multi-user.target
   ```

3. Create a dedicated user:
   ```bash
   sudo useradd -r -s /bin/false cleanuparr
   ```

4. Move Cleanuparr files and set permissions:
   ```bash
   sudo mkdir -p /opt/cleanuparr
   sudo cp -r cleanuperr-linux-*/* /opt/cleanuparr/
   sudo chown -R cleanuparr:cleanuparr /opt/cleanuparr
   sudo chmod +x /opt/cleanuparr/cleanuparr
   ```

5. Enable and start the service:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable cleanuparr
   sudo systemctl start cleanuparr
   ```

## 🔧 Configuration

### Default Settings
- **Web Interface**: http://localhost:11011
- **Configuration File**: `appsettings.json`

### Configuration Locations
- **Windows Installer**: `C:\Program Files\Cleanuparr\`
- **Windows Portable**: Same directory as executable
- **macOS Installer**: `~/Library/Application Support/Cleanuparr/`
- **macOS Portable**: Same directory as executable
- **Linux**: Same directory as executable

### Common Configuration Options
Edit `appsettings.json` to customize:

```json
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
```

#### Key Settings:
- `HTTP_PORTS`: Change the web interface port (default: 11011)
- `Logging.LogLevel.Default`: Set logging verbosity (Debug, Information, Warning, Error)

## 🌐 First Run

1. **Access Web Interface**: Open http://localhost:11011 in your browser
2. **Initial Setup**: Follow the setup wizard to configure your first connections
3. **Add Applications**: Connect your *arr applications (Sonarr, Radarr, etc.)
4. **Configure Rules**: Set up cleaning rules based on your preferences

## 🔄 Updates

### Windows Installer
- Download and run the new installer
- It will automatically update the existing installation

### PKG Installer (macOS)
- Download and install the new PKG
- It will replace the existing installation

### Portable Executables
- Download the new version
- Stop the current instance
- Replace the executable
- Start the new version

## 🛠️ Troubleshooting

### Cannot Access Web Interface
1. Check if Cleanuparr is running:
   - **Windows**: Task Manager or Services
   - **macOS**: Activity Monitor
   - **Linux**: `ps aux | grep cleanuparr`

2. Check the port configuration in `appsettings.json`
3. Verify firewall settings allow the configured port
4. Check logs for errors

### Service Won't Start (Windows)
1. Run Command Prompt as Administrator
2. Check service status: `sc query Cleanuparr`
3. Check Windows Event Logs
4. Verify configuration file is valid JSON

### Permission Issues (macOS/Linux)
1. Ensure executable permissions: `chmod +x cleanuparr`
2. Check configuration directory permissions
3. For system service, verify user permissions

### Common Issues
- **Port Already in Use**: Change `HTTP_PORTS` in configuration
- **Configuration Errors**: Validate JSON syntax in `appsettings.json`
- **Network Access**: Check firewall and network settings

## 🆘 Getting Help

- **Documentation**: https://github.com/flmorg/cleanuperr
- **Issues**: https://github.com/flmorg/cleanuperr/issues
- **Discussions**: https://github.com/flmorg/cleanuperr/discussions

## 🗑️ Uninstallation

### Windows Installer
- Use "Add or Remove Programs" in Windows Settings
- Or run the uninstaller from the Start Menu

### macOS PKG
- Delete `/Applications/Cleanuparr.app`
- Remove `~/Library/Application Support/Cleanuparr`

### Portable Executables
- Stop the application
- Delete the extracted folder
- Remove any created configuration directories 