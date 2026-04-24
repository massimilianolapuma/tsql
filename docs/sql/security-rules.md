# T-SQL — Regole di sicurezza Contoso

Riferimento per il reviewer agentico (`.github/workflows/tsql-agentic-review.md`)
e per chi scrive codice T-SQL nel progetto. Le regole **deterministiche** sono
implementate come custom rule DacFx (`tools/Contoso.SqlCodeAnalysis.Rules/`) o
regole `SR*` built-in. Qui descriviamo i controlli **semantici** che il reviewer
agentico deve verificare.

## SQL Injection / Dynamic SQL

- Mai concatenare input in una stringa eseguita con `EXEC(@sql)`. Usare sempre
  `sp_executesql` con parametri tipizzati.
- Regola deterministica: `Contoso.Security.CT0001` flagga concatenazione diretta
  in `EXEC`. **Non duplicarla nei commenti**.
- Il reviewer deve segnalare casi che sfuggono alla regola, ad esempio:
  - Costruzione del comando in più passaggi (`SET @sql = @sql + @userInput`).
  - Branching condizionale che concatena solo in alcuni rami.
  - Uso di `QUOTENAME` su valori scalari (non protegge da SQLi se l'input
    finisce in una clausola `WHERE` come literal).

## Permission grants

- `GRANT ... TO PUBLIC` non è ammesso. Usare ruoli dedicati
  (`db_app_reader`, `db_app_writer`, ruoli applicativi `app_<service>`).
- `GRANT CONTROL`, `ALTER ANY`, `IMPERSONATE` richiedono approvazione del
  team Security.
- Le SP che leggono PII devono essere accessibili solo via ruolo `pii_reader`.

## Audit & PII

- Ogni SP che legge da tabelle marcate `PII` o `SENSITIVE` (vedi
  `docs/sql/compliance.md`) deve scrivere in `audit.AccessLog`.
- Il reviewer agentico deve flaggare SP che toccano `pii.*` senza chiamare
  `audit.usp_LogAccess`.

## Crypto

- Niente algoritmi deboli (`MD5`, `SHA1`) per password o token.
- Per dati at-rest sensibili usare Always Encrypted o TDE; mai roll-your-own.
