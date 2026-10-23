set shell := ["pwsh.exe", "-c"]

# Default recipe: build Release configuration and deploy to local mods folder
default: build

# Build CombatSolver DLL and helper tools in Release mode
build:
    dotnet build CombatSolver.csproj -c Release

# Build CombatSolver in Debug mode
build-debug:
    dotnet build CombatSolver.csproj -c Debug

# Clean build artifacts
clean:
    dotnet clean CombatSolver.csproj

