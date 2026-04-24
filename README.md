# tsql — esempio analisi statica T-SQL

Esempio end-to-end di pipeline di analisi statica per progetti T-SQL basata su
`Microsoft.Build.Sql`, regole `SR*` built-in, regole custom in .NET e upload
SARIF su GitHub code scanning. La parte "semantica" è confinata lato dev
(chat mode VS Code + Copilot coding agent sulle PR), **niente LLM in CI**.

## Layout

```
src/Database/                       progetto SQL (SDK-style .sqlproj)
  Database.sqlproj                  policy regole + custom rules ref
  StaticCodeAnalysis.SuppressMessages.xml

tools/Contoso.SqlCodeAnalysis.Rules/        regole T-SQL custom .NET
  NoConcatenatedDynamicSqlRule.cs           CT0001 — vieta dynamic SQL concatenato
  RuleResources.resx
  Contoso.SqlCodeAnalysis.Rules.csproj      pacchetto NuGet locale

tools/Contoso.SqlCodeAnalysis.Rules.Tests/  xUnit per le regole
tools/sql-analysis/build_log_to_sarif.py    converter MSBuild -> SARIF 2.1.0

.github/
  workflows/sql-static-analysis.yml         pipeline CI
  chatmodes/tsql-reviewer.chatmode.md       chat mode VS Code lato dev
  copilot-instructions.md                   istruzioni per Copilot coding agent
```

## Build & test (locale)

```bash
# 1. compila + esegui i test della regola custom
dotnet test tools/Contoso.SqlCodeAnalysis.Rules.Tests

# 2. impacchetta la regola come NuGet locale (la consuma il .sqlproj)
dotnet pack tools/Contoso.SqlCodeAnalysis.Rules -c Release -o artifacts/nupkgs

# 3. analizza il database (CI=true => warning -> error)
CI=true dotnet build src/Database/Database.sqlproj -c Release
```

## Severity gating
- **Bloccante in CI:** errori `SR*` elevati con `+!` in `Database.sqlproj` e qualsiasi
  finding `Contoso.Security.*`.
- **Advisory:** warning `SR*` non elevati e commenti del Copilot agent sulla PR.

## Suppression
Solo via `src/Database/StaticCodeAnalysis.SuppressMessages.xml` con motivazione,
owner e (se temporanea) issue di tracking. Vedi il file per il template.
