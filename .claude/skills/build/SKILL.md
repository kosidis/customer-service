---
name: build
description: WHEN building the solution, adding or upgrading NuGet packages, or fixing build warnings in this .NET 10 repo.
---

# Build

Build system: .NET SDK 10.0.x via the `dotnet` CLI. Solution: `apps/api/CustomerService.slnx` (the XML solution format). Local tools: `dotnet-tools.json` (`dotnet-ef` 10.0.14).

```bash
dotnet tool restore          # once per clone
dotnet build apps/api                 # whole solution, Debug
dotnet build apps/api -c Release
dotnet publish apps/api/src/CustomerService.Api -c Release -o <output-dir> /p:UseAppHost=false   # what the Dockerfile runs
```

The build is warning-free today. Keep it that way: treat any new warning, especially `NU190x` vulnerability warnings, as a failure to fix.

## Projects

- `apps/api/src/CustomerService.Api` (`Microsoft.NET.Sdk.Web`), `net10.0`, `Nullable` and `ImplicitUsings` enabled.
- `apps/api/tests/CustomerService.UnitTests` and `apps/api/tests/CustomerService.IntegrationTests` reference the API project.

## Packages

- Add: `dotnet add apps/api/src/CustomerService.Api package <PackageId>`. Versions are pinned in each csproj; there's no central package management.
- Keep every `Microsoft.EntityFrameworkCore.*` and `Microsoft.AspNetCore.*` package on the same version (10.0.14 today) across all three projects.
- `System.Security.Cryptography.Pkcs` 10.0.14 is pinned directly in the API csproj because `Microsoft.Data.SqlClient` 6.1.7 pulls in vulnerable 9.0.11 (GHSA-2fmv-rv2c-59x7, GHSA-846f-f764-76mx). Check with `dotnet nuget why apps/api/src/CustomerService.Api System.Security.Cryptography.Pkcs`; remove the pin once SqlClient depends on a patched version.
- Check for vulnerable packages: `dotnet list package --vulnerable --include-transitive`.
