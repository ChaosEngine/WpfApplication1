# .NET 10.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that an .NET 10.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10.0 upgrade.
3. Upgrade WpfApplication1\WpfApplication1.csproj

## Settings

This section contains settings and data used by execution steps.

### Excluded projects

| Project name                                   | Description                 |
|:-----------------------------------------------|:---------------------------:|

### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                        | Current Version | New Version | Description                                   |
|:------------------------------------|:---------------:|:-----------:|:----------------------------------------------|
| AvalonEdit                          |   6.3.1.120     |  6.2.0.78   | Incompatible with .NET 10.0                   |
| Microsoft.Bcl.AsyncInterfaces       |   9.0.9         |             | Remove and replace with Microsoft.Bcl.AsyncInterfaces 10.0.0-rc.2.25502.107 |
| Microsoft.Bcl.AsyncInterfaces       |                 | 10.0.0-rc.2.25502.107 | Replacement for old version                   |
| System.Text.Json                    |   9.0.9         |             | Remove and replace with System.Text.Json 10.0.0-rc.2.25502.107 |
| System.Text.Json                    |                 | 10.0.0-rc.2.25502.107 | Replacement for old version                   |
| System.Linq                         |   4.3.0         |             | Functionality included with framework reference |
| System.Net.Http                     |   4.3.4         |             | Functionality included with framework reference |
| System.Reflection                   |   4.3.0         |             | Functionality included with framework reference |
| System.Runtime                      |   4.3.1         |             | Functionality included with framework reference |
| System.Security.Cryptography.Algorithms | 4.3.1         |             | Functionality included with framework reference |
| System.Security.Cryptography.X509Certificates | 4.3.2         |             | Functionality included with framework reference |
| System.Text.Encoding                |   4.3.0         |             | Functionality included with framework reference |

### Project upgrade details

#### WpfApplication1\WpfApplication1.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0-windows` to `net10.0-windows`

NuGet packages changes:
  - AvalonEdit should be updated from `6.3.1.120` to `6.2.0.78` (incompatible with .NET 10.0)
  - Microsoft.Bcl.AsyncInterfaces should be removed and replaced with `10.0.0-rc.2.25502.107`
  - System.Text.Json should be removed and replaced with `10.0.0-rc.2.25502.107`
  - System.Linq, System.Net.Http, System.Reflection, System.Runtime, System.Security.Cryptography.Algorithms, System.Security.Cryptography.X509Certificates, System.Text.Encoding should be removed (functionality included with framework reference)

Other changes:
  - Ensure all code and project references are compatible with .NET 10.0
