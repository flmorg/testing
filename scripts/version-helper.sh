#!/bin/bash
set -e

# Version Helper Script for Cleanuparr
# This script provides consistent version handling across all build workflows

# Function to extract version from git ref
get_version_from_ref() {
    local ref="$1"
    local default_version="0.0.1-dev"
    
    if [[ "$ref" =~ ^refs/tags/v?(.+)$ ]]; then
        # Extract version from tag, removing 'v' prefix if present
        local version="${BASH_REMATCH[1]}"
        echo "$version"
    else
        # Not a tag, return default dev version
        echo "$default_version"
    fi
}

# Function to create a dev version with timestamp
get_dev_version() {
    local timestamp=$(date +%Y%m%d-%H%M%S)
    echo "0.0.1-dev-$timestamp"
}

# Function to validate version format
validate_version() {
    local version="$1"
    
    # Basic semver validation (simplified)
    if [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-.*)?$ ]]; then
        return 0  # Valid
    else
        return 1  # Invalid
    fi
}

# Function to get release version (with v prefix for tags)
get_release_version() {
    local ref="$1"
    
    if [[ "$ref" =~ ^refs/tags/ ]]; then
        # Extract tag name
        local tag="${ref##refs/tags/}"
        # If tag doesn't start with 'v', add it
        if [[ ! "$tag" =~ ^v ]]; then
            echo "v$tag"
        else
            echo "$tag"
        fi
    else
        # Not a tag, create dev release version
        local dev_version=$(get_dev_version)
        echo "v$dev_version"
    fi
}

# Function to check if this is a tag event
is_tag_event() {
    local ref="$1"
    [[ "$ref" =~ ^refs/tags/ ]]
}

# Function to check if this is a pre-release version
is_prerelease() {
    local version="$1"
    [[ "$version" =~ -.*$ ]]
}

# Main function for getting version info
get_version_info() {
    local ref="$1"
    local event_name="$2"
    
    local app_version
    local release_version
    local is_tag=false
    local is_prerelease=false
    
    if is_tag_event "$ref"; then
        is_tag=true
        app_version=$(get_version_from_ref "$ref")
        release_version=$(get_release_version "$ref")
        
        if is_prerelease "$app_version"; then
            is_prerelease=true
        fi
    else
        # Not a tag event
        if [[ "$event_name" == "workflow_dispatch" ]]; then
            app_version=$(get_dev_version)
        else
            app_version="0.0.1-dev"
        fi
        release_version="v$app_version"
    fi
    
    # Validate version
    if ! validate_version "$app_version"; then
        echo "❌ Invalid version format: $app_version" >&2
        exit 1
    fi
    
    # Output version info as environment variables format
    echo "APP_VERSION=$app_version"
    echo "RELEASE_VERSION=$release_version"
    echo "IS_TAG=$is_tag"
    echo "IS_PRERELEASE=$is_prerelease"
}

# Command line interface
case "${1:-}" in
    "get-version")
        get_version_from_ref "${2:-}"
        ;;
    "get-dev-version")
        get_dev_version
        ;;
    "get-release-version")
        get_release_version "${2:-}"
        ;;
    "is-tag")
        if is_tag_event "${2:-}"; then
            echo "true"
        else
            echo "false"
        fi
        ;;
    "is-prerelease")
        if is_prerelease "${2:-}"; then
            echo "true"
        else
            echo "false"
        fi
        ;;
    "version-info")
        get_version_info "${2:-}" "${3:-}"
        ;;
    "validate")
        if validate_version "${2:-}"; then
            echo "✅ Valid version: ${2:-}"
        else
            echo "❌ Invalid version: ${2:-}"
            exit 1
        fi
        ;;
    *)
        echo "Usage: $0 {get-version|get-dev-version|get-release-version|is-tag|is-prerelease|version-info|validate} [args...]"
        echo ""
        echo "Commands:"
        echo "  get-version <ref>           - Extract version from git ref"
        echo "  get-dev-version            - Generate dev version with timestamp"
        echo "  get-release-version <ref>  - Get release version (with v prefix)"
        echo "  is-tag <ref>              - Check if ref is a tag"
        echo "  is-prerelease <version>   - Check if version is pre-release"
        echo "  version-info <ref> <event> - Get all version info as env vars"
        echo "  validate <version>        - Validate version format"
        exit 1
        ;;
esac 