# Cleanuparr Build System Testing Guide

This guide helps you test the newly implemented build system to ensure everything works correctly.

## 🧪 Testing Overview

The build system consists of 5 main workflows:
1. `build_executable.yml` - Portable executables for all platforms
2. `build-windows-installer.yml` - Windows installer
3. `build-macos-intel-installer.yml` - macOS Intel installer  
4. `build-macos-arm-installer.yml` - macOS ARM installer
5. `release.yml` - Comprehensive release workflow (orchestrates all builds)

## 🔧 Pre-Testing Checklist

Before testing, ensure:
- [ ] All files have been committed to your repository
- [ ] GitHub secrets are properly configured:
  - [ ] `VAULT_HOST`
  - [ ] `VAULT_ROLE_ID` 
  - [ ] `VAULT_SECRET_ID`
- [ ] The vault contains required secrets:
  - [ ] `secrets/data/github/repo_readonly_pat`
  - [ ] `secrets/data/github/packages_pat`

## 🚀 Testing Procedures

### Phase 1: Individual Workflow Testing

Test each workflow individually using manual dispatch:

#### 1.1 Test Portable Executables
1. Go to **Actions** → **Build Executables**
2. Click **Run workflow**
3. Leave version empty (will use dev version)
4. Click **Run workflow**
5. **Expected**: 5 ZIP files containing executables for all platforms

#### 1.2 Test Windows Installer
1. Go to **Actions** → **Build Windows Installer**
2. Click **Run workflow**
3. **Expected**: Windows installer EXE file

#### 1.3 Test macOS Intel Installer
1. Go to **Actions** → **Build macOS Intel Installer**
2. Click **Run workflow**
3. **Expected**: PKG installer for Intel Macs

#### 1.4 Test macOS ARM Installer
1. Go to **Actions** → **Build macOS ARM Installer**
2. Click **Run workflow**
3. **Expected**: PKG installer for Apple Silicon

### Phase 2: Release Workflow Testing

#### 2.1 Manual Release Test
1. Go to **Actions** → **Release Build**
2. Click **Run workflow**
3. Optionally enter a version (e.g., `1.0.0-test`)
4. Click **Run workflow**
5. **Expected**: All 4 build workflows run in parallel, creating all artifacts

#### 2.2 Tag Release Test
1. Create and push a test tag:
   ```bash
   git tag v1.0.0-test
   git push origin v1.0.0-test
   ```
2. **Expected**: 
   - All workflows automatically triggered
   - GitHub release created with all artifacts
   - Release notes automatically generated

## ✅ Validation Checklist

For each successful test run, verify:

### Portable Executables
- [ ] Windows executable (`cleanuperr-win-amd64.zip`)
- [ ] Linux AMD64 executable (`cleanuperr-linux-amd64.zip`)
- [ ] Linux ARM64 executable (`cleanuperr-linux-arm64.zip`)
- [ ] macOS Intel executable (`cleanuperr-osx-amd64.zip`)
- [ ] macOS ARM executable (`cleanuperr-osx-arm64.zip`)
- [ ] Each contains: executable, `appsettings.json`, `README.md`, `LICENSE`

### Windows Installer
- [ ] Installer file created (`Cleanuparr-{version}-Setup.exe`)
- [ ] File size reasonable (50-100MB)
- [ ] Artifact uploaded successfully

### macOS Installers
- [ ] Intel PKG created (`Cleanuparr-{version}-macos-intel.pkg`)
- [ ] ARM PKG created (`Cleanuparr-{version}-macos-arm64.pkg`)
- [ ] File sizes reasonable (50-100MB each)

### Release Workflow
- [ ] All 4 sub-workflows completed successfully
- [ ] All artifacts collected and attached to release
- [ ] Release notes generated properly
- [ ] Version numbers consistent across all artifacts

## 🐛 Troubleshooting

### Common Issues

#### Build Failures
**Symptoms**: Workflow fails during .NET build
**Solutions**:
- Check if secrets are properly configured
- Verify NuGet package access
- Review .NET project files for issues

#### Frontend Build Issues
**Symptoms**: Angular build fails
**Solutions**:
- Check Node.js version in workflows (should be 18)
- Verify `package.json` and `package-lock.json` are correct
- Ensure frontend path is `code/frontend`

#### Artifact Upload Issues
**Symptoms**: Artifacts not uploaded or accessible
**Solutions**:
- Check file paths in workflows
- Verify artifact names match between workflows
- Ensure proper permissions

#### Version Issues
**Symptoms**: Inconsistent versions across artifacts
**Solutions**:
- Check version extraction logic
- Verify tag format (should be `v1.0.0`)
- Review version helper scripts

### Debugging Steps

1. **Check Workflow Logs**:
   - Click on failed workflow run
   - Expand failed steps
   - Look for specific error messages

2. **Verify Secrets**:
   - Go to repository Settings → Secrets
   - Ensure all required secrets exist
   - Test vault access if possible

3. **Check File Paths**:
   - Verify all referenced files exist
   - Check case sensitivity
   - Ensure relative paths are correct

4. **Test Locally** (if possible):
   - Try building frontend: `cd code/frontend && npm ci && npm run build`
   - Try building backend: `dotnet build code/backend/Cleanuparr.Api`

## 📊 Testing Results Template

Copy and fill out this template for your testing:

```
## Build System Test Results

**Date**: [DATE]
**Tester**: [NAME]
**Repository**: [REPO_URL]

### Individual Workflow Tests
- [ ] Build Executables: ✅/❌
- [ ] Windows Installer: ✅/❌
- [ ] macOS Intel Installer: ✅/❌
- [ ] macOS ARM Installer: ✅/❌

### Release Workflow Tests
- [ ] Manual Release: ✅/❌
- [ ] Tag Release: ✅/❌

### Artifact Validation
- [ ] All executables created: ✅/❌
- [ ] Windows installer works: ✅/❌
- [ ] macOS installers work: ✅/❌
- [ ] Release notes correct: ✅/❌

### Issues Found
[List any issues discovered]

### Overall Status
- [ ] Ready for production ✅
- [ ] Needs minor fixes ⚠️
- [ ] Needs major fixes ❌
```

## 🎯 Success Criteria

The build system is ready for production when:
- [ ] All individual workflows complete successfully
- [ ] Release workflow orchestrates all builds correctly
- [ ] All expected artifacts are generated
- [ ] Artifacts are properly named and versioned
- [ ] GitHub releases are created automatically from tags
- [ ] No critical errors in any workflow logs

## 🚀 Next Steps After Testing

Once testing is complete and successful:
1. **Update Documentation**: Fix any issues found during testing
2. **Create Production Release**: Use a real version tag (e.g., `v1.0.0`)
3. **Validate Installers**: Test actual installation on target platforms
4. **Monitor First Release**: Watch for any issues with the first production release

---

**Need Help?** Check the main repository documentation or create an issue for build system problems. 