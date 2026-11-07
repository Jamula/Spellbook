# Spellbook development guidelines

- Track work in GitHub issues; link each PR to an issue and update the issue when delivered.
- Use .NET 10 and Blazor WebAssembly. Keep iPhone Safari a first-class target.
- Core functionality must have **$0 recurring infrastructure cost**. Do not provision billable services, enable paid APIs, or commit cloud credentials without explicit owner approval.
- Prefer browser-local deck data plus user-controlled JSON export; explain that browser storage does not sync across devices.
- Do not upload user photos/videos automatically. Require clear consent before any remote processing.
- Before a PR, run `dotnet restore Spellbook/Spellbook.csproj`, `dotnet build Spellbook/Spellbook.csproj -c Release`, and `dotnet publish Spellbook/Spellbook.csproj -c Release`.
- Reference the roadmap in #1 and use focused, testable tasks (#2-#8).
