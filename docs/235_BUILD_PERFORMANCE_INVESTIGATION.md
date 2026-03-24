# 🔍 KasserPro Build Performance Investigation Plan

## 📊 Current Analysis
- **Project Type:** Multi-layer .NET 8 Enterprise Application
- **Architecture:** API → Application → Infrastructure → Domain
- **Database:** SQLite with EF Core 8.0.11
- **Issue:** Build time 10-12 minutes (previously normal)
- **Symptoms:** Stuck at "Building..." phase, no errors, only warnings

---

## 🎯 Phase 1: MEASURE - Baseline Performance Analysis

### 1.1 Enable Detailed Build Logging
```bash
# Navigate to backend directory
cd backend

# Clean everything first
dotnet clean --verbosity normal
dotnet nuget locals all --clear

# Enable detailed MSBuild logging
dotnet build --verbosity diagnostic > build-diagnostic.log 2>&1

# Performance summary (faster alternative)
dotnet build -clp:PerformanceSummary --verbosity normal
```

**Expected Output Analysis:**
- Look for tasks taking >30 seconds
- Identify bottleneck in ResolveAssemblyReferences, CoreCompile, or ResolvePackageAssets

### 1.2 Measure Individual Project Build Times
```bash
# Test each project individually
echo "=== Domain Project ===" 
time dotnet build KasserPro.Domain/KasserPro.Domain.csproj --no-restore

echo "=== Application Project ===" 
time dotnet build KasserPro.Application/KasserPro.Application.csproj --no-restore

echo "=== Infrastructure Project ===" 
time dotnet build KasserPro.Infrastructure/KasserPro.Infrastructure.csproj --no-restore

echo "=== API Project ===" 
time dotnet build KasserPro.API/KasserPro.API.csproj --no-restore
```

### 1.3 Binary Build Log Analysis
```bash
# Generate binary build log for detailed analysis
dotnet msbuild KasserPro.API/KasserPro.API.csproj /bl:build-analysis.binlog

# View with MSBuild Structured Log Viewer (if available)
# Or analyze manually for slow tasks
```

### 1.4 Check for Circular References
```bash
# List all project references
dotnet list KasserPro.API/KasserPro.API.csproj reference
dotnet list KasserPro.Application/KasserPro.Application.csproj reference  
dotnet list KasserPro.Infrastructure/KasserPro.Infrastructure.csproj reference
dotnet list KasserPro.Domain/KasserPro.Domain.csproj reference

# Check for potential circular dependencies
echo "=== Reference Chain Analysis ==="
echo "API → Application, Infrastructure"
echo "Infrastructure → Domain, Application" 
echo "Application → Domain"
echo "Domain → (none)"
```

---

## 🔬 Phase 2: ISOLATE - Identify Root Cause

### 2.1 Test Without Restore
```bash
# Skip package restore to isolate compilation issues
dotnet build --no-restore --verbosity normal
```

### 2.2 Single-Threaded Build Test
```bash
# Force single-threaded build to avoid concurrency issues
dotnet build /m:1 --verbosity normal
```

### 2.3 EF Core Model Compilation Test
```bash
# Test if EF model building is the bottleneck
# Temporarily comment out DbContext registration in Program.cs
# Then test build time
```

### 2.4 Source Generator Impact Test
```bash
# Check for source generators causing slowdown
dotnet build -p:UseSourceLink=false -p:IncludeSourceRevisionInInformationalVersion=false
```

### 2.5 Assembly Scanning Test
```bash
# Test if DI container scanning is slow during build
# Check Program.cs for heavy reflection-based registrations
```

---

## 🛠️ Phase 3: FIX - Performance Optimizations

### 3.1 Optimize Project Files (.csproj)

#### KasserPro.API.csproj Optimizations:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    
    <!-- Build Performance Optimizations -->
    <UseSharedCompilation>true</UseSharedCompilation>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
    
    <!-- Reduce Assembly Scanning -->
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
    
    <!-- Skip Unnecessary Tasks -->
    <SkipCopyBuildProduct>true</SkipCopyBuildProduct>
    <CopyBuildOutputToOutputDirectory>false</CopyBuildOutputToOutputDirectory>
    
    <!-- Parallel Build -->
    <BuildInParallel>true</BuildInParallel>
  </PropertyGroup>
</Project>
```

#### Apply to All Projects:
```xml
<!-- Add to Directory.Build.props in backend folder -->
<Project>
  <PropertyGroup>
    <UseSharedCompilation>true</UseSharedCompilation>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
    <BuildInParallel>true</BuildInParallel>
  </PropertyGroup>
</Project>
```

### 3.2 Optimize DbContext Compilation

#### Split Large DbContext:
```csharp
// Create partial DbContext files to reduce compilation unit size
public partial class KasserproContext : DbContext
{
    // Keep only essential DbSets here
    public virtual DbSet<Order> Orders { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    // Move others to partial files
}

// KasserproContext.Inventory.cs
public partial class KasserproContext
{
    public virtual DbSet<BranchInventory> BranchInventories { get; set; }
    public virtual DbSet<StockMovement> StockMovements { get; set; }
}

// KasserproContext.Financial.cs  
public partial class KasserproContext
{
    public virtual DbSet<Payment> Payments { get; set; }
    public virtual DbSet<Expense> Expenses { get; set; }
}
```

#### Optimize OnModelCreating:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Use assembly scanning instead of manual configuration
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(KasserproContext).Assembly);
    
    OnModelCreatingPartial(modelBuilder);
}
```

### 3.3 Package Lock File Generation
```bash
# Generate package lock files to speed up restore
dotnet restore --use-lock-file

# This creates packages.lock.json files
# Commit these to source control
```

### 3.4 NuGet Cache Optimization
```bash
# Clear and optimize NuGet cache
dotnet nuget locals all --clear
dotnet nuget locals global-packages --list

# Set local NuGet cache (optional)
# Add to nuget.config:
```

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <add key="globalPackagesFolder" value="./packages" />
  </config>
</configuration>
```

---

## 🔧 Immediate Action Checklist

### ✅ Execute These Commands Now:

1. **Baseline Measurement:**
```bash
cd backend
dotnet clean
dotnet build -clp:PerformanceSummary --verbosity normal
```

2. **Individual Project Timing:**
```bash
time dotnet build KasserPro.Domain/KasserPro.Domain.csproj --no-restore
time dotnet build KasserPro.Application/KasserPro.Application.csproj --no-restore  
time dotnet build KasserPro.Infrastructure/KasserPro.Infrastructure.csproj --no-restore
time dotnet build KasserPro.API/KasserPro.API.csproj --no-restore
```

3. **Check References:**
```bash
dotnet list KasserPro.API/KasserPro.API.csproj reference
```

4. **Test Single-Threaded:**
```bash
dotnet build /m:1 --verbosity normal
```

5. **Clear Cache:**
```bash
dotnet nuget locals all --clear
```

---

## 🎯 Expected Results & Next Steps

### If Domain/Application builds fast but Infrastructure/API is slow:
- **Root Cause:** EF Core model compilation or heavy DI registration
- **Solution:** Split DbContext, optimize Program.cs

### If all projects are slow:
- **Root Cause:** Package resolution or assembly scanning
- **Solution:** Package lock files, optimize project files

### If single-threaded is faster:
- **Root Cause:** Concurrency issues or resource contention  
- **Solution:** Adjust MSBuild parallelism settings

### If cache clear helps temporarily:
- **Root Cause:** Corrupted NuGet cache or package conflicts
- **Solution:** Package lock files, clean restore process

---

## 🚨 Critical Checks

### Antivirus Interference:
```bash
# Check if build folder is being scanned
# Add these to antivirus exclusions:
# - backend/*/bin/
# - backend/*/obj/  
# - %USERPROFILE%\.nuget\packages\
```

### Disk I/O Bottleneck:
```bash
# Monitor during build:
# - Task Manager → Performance → Disk
# - Look for 100% disk usage during build
```

### Memory Pressure:
```bash
# Monitor during build:
# - Task Manager → Performance → Memory
# - Look for >80% memory usage
```

---

## 📈 Success Metrics

- **Target:** Build time under 2 minutes
- **Acceptable:** Build time under 5 minutes  
- **Current:** 10-12 minutes (needs immediate fix)

Execute Phase 1 commands first, then report results for targeted Phase 2 investigation.