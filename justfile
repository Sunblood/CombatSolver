set shell := ["pwsh.exe", "-c"]

# Default recipe: build Release configuration and deploy to local mods folder
default: build

# Build CombatSolver DLL and helper tools in Release mode
build:
    dotnet build CombatSolver.csproj -c Release

# Build CombatSolver in Debug mode
build-debug:
    dotnet build CombatSolver.csproj -c Debug

# Close STS2, build & deploy the mod, and relaunch STS2
build-launch:
    Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue | Stop-Process -Force; Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue | Wait-Process -ErrorAction SilentlyContinue; exit 0
    dotnet build CombatSolver.csproj -c Release
    Start-Process "steam://rungameid/2868840"

# Clean build artifacts
clean:
    dotnet clean CombatSolver.csproj


