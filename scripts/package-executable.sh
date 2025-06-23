#!/bin/bash
set -e

# Package Executable Script for Cleanuparr
# This script packages built executables with configuration files and documentation

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Function to create sample configuration
create_sample_config() {
    local config_file="$1"
    
    cat > "$config_file" << 'EOF'
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
}

# Function to create README
create_readme() {
    local readme_file="$1"
    local platform="$2"
    local version="$3"
    
    cat > "$readme_file" << EOF
# Cleanuparr v$version

## Overview
Cleanuparr is a download management tool that works with *arr applications (Sonarr, Radarr, Lidarr, etc.) to clean up downloads based on configurable rules.

## Platform: $platform

## Quick Start

### Running Cleanuparr
1. Extract the archive to your desired location
2. Review and modify \`appsettings.json\` if needed
3. Run the executable:
   - Windows: \`cleanuparr.exe\`
   - Linux/macOS: \`./cleanuparr\`
4. Open your web browser to: http://localhost:11011

### Configuration
The \`appsettings.json\` file contains the main configuration options:
- **HTTP_PORTS**: Port number for the web interface (default: 11011)
- **Logging**: Configure logging levels and output

### Web Interface
Once running, access the web interface at:
- Local: http://localhost:11011
- Network: http://YOUR_IP:11011

## Directory Structure
\`\`\`
cleanuparr/
├── cleanuparr${platform_extension}    # Main executable
├── appsettings.json                   # Configuration file
├── README.md                         # This file
└── wwwroot/                          # Web interface files (embedded)
\`\`\`

## Support
- Documentation: https://github.com/flmorg/cleanuperr
- Issues: https://github.com/flmorg/cleanuperr/issues

## License
See LICENSE file for details.

---
Cleanuparr v$version - Built $(date)
EOF
}

# Function to package executable
package_executable() {
    local platform="$1"
    local version="$2"
    local executable_dir="$3"
    local output_dir="$4"
    
    echo "📦 Packaging executable for $platform"
    
    # Determine platform-specific details
    local platform_extension=""
    local executable_name="cleanuparr"
    
    case "$platform" in
        "win-amd64")
            platform_extension=".exe"
            executable_name="cleanuparr.exe"
            ;;
        "linux-amd64"|"linux-arm64"|"osx-amd64"|"osx-arm64")
            platform_extension=""
            executable_name="cleanuparr"
            ;;
        *)
            echo "❌ Unknown platform: $platform"
            exit 1
            ;;
    esac
    
    # Check if executable exists
    local executable_path="$executable_dir/$executable_name"
    if [ ! -f "$executable_path" ]; then
        echo "❌ Executable not found: $executable_path"
        exit 1
    fi
    
    # Create package directory
    local package_dir="$output_dir/Cleanuparr-$platform"
    mkdir -p "$package_dir"
    
    # Copy executable
    echo "📄 Copying executable: $executable_name"
    cp "$executable_path" "$package_dir/"
    
    # Make executable on Unix-like systems
    if [[ "$platform" != "win-amd64" ]]; then
        chmod +x "$package_dir/$executable_name"
    fi
    
    # Create configuration file
    echo "⚙️  Creating configuration file"
    create_sample_config "$package_dir/appsettings.json"
    
    # Create README
    echo "📖 Creating README"
    create_readme "$package_dir/README.md" "$platform" "$version"
    
    # Copy LICENSE if it exists
    if [ -f "$PROJECT_ROOT/LICENSE" ]; then
        echo "📜 Copying LICENSE"
        cp "$PROJECT_ROOT/LICENSE" "$package_dir/"
    fi
    
    echo "✅ Package created: $package_dir"
}

# Function to create archive
create_archive() {
    local platform="$1"
    local output_dir="$2"
    
    local package_dir="$output_dir/Cleanuparr-$platform"
    local archive_name="Cleanuparr-$platform.zip"
    
    if [ ! -d "$package_dir" ]; then
        echo "❌ Package directory not found: $package_dir"
        exit 1
    fi
    
    echo "🗜️  Creating archive: $archive_name"
    
    # Change to output directory for relative paths in archive
    cd "$output_dir"
    
    # Create zip archive
    zip -r "$archive_name" "Cleanuparr-$platform/" > /dev/null
    
    echo "✅ Archive created: $output_dir/$archive_name"
}

# Function to verify package
verify_package() {
    local platform="$1"
    local output_dir="$2"
    
    local package_dir="$output_dir/Cleanuparr-$platform"
    local archive_path="$output_dir/Cleanuparr-$platform.zip"
    
    echo "🔍 Verifying package for $platform"
    
    # Check package directory
    if [ ! -d "$package_dir" ]; then
        echo "❌ Package directory missing: $package_dir"
        return 1
    fi
    
    # Check required files
    local required_files=("README.md" "appsettings.json")
    
    # Add platform-specific executable
    case "$platform" in
        "win-amd64")
            required_files+=("cleanuparr.exe")
            ;;
        *)
            required_files+=("cleanuparr")
            ;;
    esac
    
    for file in "${required_files[@]}"; do
        if [ ! -f "$package_dir/$file" ]; then
            echo "❌ Required file missing: $file"
            return 1
        fi
    done
    
    # Check archive
    if [ ! -f "$archive_path" ]; then
        echo "❌ Archive missing: $archive_path"
        return 1
    fi
    
    echo "✅ Package verification passed for $platform"
    return 0
}

# Main function
main() {
    local command="$1"
    
    case "$command" in
        "package")
            local platform="$2"
            local version="$3"
            local executable_dir="$4"
            local output_dir="$5"
            
            if [ -z "$platform" ] || [ -z "$version" ] || [ -z "$executable_dir" ] || [ -z "$output_dir" ]; then
                echo "Usage: $0 package <platform> <version> <executable_dir> <output_dir>"
                exit 1
            fi
            
            package_executable "$platform" "$version" "$executable_dir" "$output_dir"
            ;;
        "archive")
            local platform="$2"
            local output_dir="$3"
            
            if [ -z "$platform" ] || [ -z "$output_dir" ]; then
                echo "Usage: $0 archive <platform> <output_dir>"
                exit 1
            fi
            
            create_archive "$platform" "$output_dir"
            ;;
        "verify")
            local platform="$2"
            local output_dir="$3"
            
            if [ -z "$platform" ] || [ -z "$output_dir" ]; then
                echo "Usage: $0 verify <platform> <output_dir>"
                exit 1
            fi
            
            verify_package "$platform" "$output_dir"
            ;;
        "full")
            local platform="$2"
            local version="$3"
            local executable_dir="$4"
            local output_dir="$5"
            
            if [ -z "$platform" ] || [ -z "$version" ] || [ -z "$executable_dir" ] || [ -z "$output_dir" ]; then
                echo "Usage: $0 full <platform> <version> <executable_dir> <output_dir>"
                exit 1
            fi
            
            package_executable "$platform" "$version" "$executable_dir" "$output_dir"
            create_archive "$platform" "$output_dir"
            verify_package "$platform" "$output_dir"
            ;;
        *)
            echo "Usage: $0 {package|archive|verify|full} <args...>"
            echo ""
            echo "Commands:"
            echo "  package <platform> <version> <executable_dir> <output_dir>"
            echo "  archive <platform> <output_dir>"
            echo "  verify <platform> <output_dir>"
            echo "  full <platform> <version> <executable_dir> <output_dir>"
            echo ""
            echo "Platforms: win-amd64, linux-amd64, linux-arm64, osx-amd64, osx-arm64"
            exit 1
            ;;
    esac
}

# Run main function
main "$@" 