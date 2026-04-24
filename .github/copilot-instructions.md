# Copilot instructions — sezione T-SQL

Queste istruzioni si applicano a tutte le PR che modificano file sotto
`src/Database/**` o `tools/Contoso.SqlCodeAnalysis.Rules/**`.

## Contesto
- Linguaggio: T-SQL (SQL Server 2019+, DSP `Sql160`).
- Build: SDK-style `.sqlproj` con `Microsoft.Build.Sql` 1.0.0.
- Analisi statica deterministica: regole `SR*` built-in + regole custom `Contoso.*`
  (vedi `tools/Contoso.SqlCodeAnalysis.Rules/`). Eseguita dal workflow
  `sql-static-analysis.yml`. **Non duplicare in chat ciò che è già flaggato lì.**
- Suppression centralizzate in `src/Database/StaticCodeAnalysis.SuppressMessages.xml`.

## Quando rivedi una PR T-SQL
1. Leggi prima `docs/sql/conventions.md`, `docs/sql/security-rules.md`,
   `docs/sql/performance-rules.md`, `docs/sql/compliance.md`.
2. Concentra i commenti sui temi **semantici** che le regole non coprono:
   - logica di business errata o ambigua;
   - mancato uso di transazioni dove serve atomicità;
   - mancato log di audit per accessi a dati PII;
   - permessi `GRANT`/`REVOKE` troppo larghi rispetto al ruolo;
   - dynamic SQL non parametrizzato che sfugge a `Contoso.Security.CT0001`.
3. Suggerisci sempre un **rimedio concreto** (snippet T-SQL), non solo l'osservazione.
4. Se proponi una nuova suppression: deve includere rule ID, motivazione, owner e
   issue di tracking, e va aggiunta a `StaticCodeAnalysis.SuppressMessages.xml`.

## Cosa NON devi fare
- Non aprire eccezioni alle regole `Contoso.Security.*` senza approvazione di
  `@security-team`.
- Non disabilitare `RunSqlCodeAnalysis` né rimuovere `+!` da regole già elevate a
  Error in `Database.sqlproj`.
- Non eseguire query contro database reali.
