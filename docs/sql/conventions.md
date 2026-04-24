# T-SQL — Convenzioni Contoso

> Scope: tutti gli oggetti sotto `src/Database/**`. Target: SQL Server 2019+, DSP `Sql160`.

Riferito da `.github/chatmodes/tsql-reviewer.chatmode.md` e dal workflow
`tsql-agentic-review`.

## Naming

- Schemi: `dbo` solo per oggetti tecnici/legacy. Ogni dominio ha il proprio
  schema (`sales`, `audit`, `staging`, `report`, `pii`).
- Tabelle: `PascalCase` singolare (`Customer`, `OrderLine`).
- Colonne: `PascalCase`. Chiavi: `<Entity>Id` (PK) / `<Referenced>Id` (FK).
- Stored procedure: `usp_<Verbo><Oggetto>` (es. `usp_GetCustomerById`).
- Funzioni: `ufn_<Scopo>` (es. `ufn_NormalizePhone`).
- Viste: `v_<Scopo>` (es. `v_ActiveCustomers`).
- Trigger: `tr_<Tabella>_<Evento>`.
- Indici: `IX_<Tabella>_<Colonne>` (filtered: aggiungi `_F`),
  `UX_` per unique, `PK_<Tabella>`, `FK_<Tabella>_<Riferimento>`.
- Parametri SP: `@PascalCase` con tipo allineato alla colonna sorgente.

## Layout

- Un oggetto per file. Path mirror dello schema:
  `src/Database/<Schema>/<Tipo>/<Nome>.sql`.
- Encoding **UTF-8 con BOM**, EOL `CRLF` (allineato a SSDT).
- Indentazione: 4 spazi, mai tab.
- Keyword in `UPPERCASE`, identificatori in `PascalCase`.
- Una statement per riga; `GO` solo a fine batch logico.

## Stile

- `SET NOCOUNT ON;` come prima istruzione di ogni SP.
- Terminatore `;` obbligatorio su ogni statement.
- `BEGIN ... END` espliciti anche per blocchi a singolo statement dentro
  `IF`/`WHILE`.
- Niente `SELECT *` fuori da CTE di staging temporaneo.
- Alias di tabella obbligatori in JOIN, prefisso colonna obbligatorio quando
  più di una tabella è in scope.

## Commenti

- Header obbligatorio per SP/funzioni:

  ```sql
  -- =====================================================================
  -- Object:      <schema>.<nome>
  -- Purpose:     <descrizione>
  -- Author:      <team>
  -- Created:     YYYY-MM-DD
  -- Notes:       <eventuali>
  -- =====================================================================
  ```

- Modifiche tracciate via git, non in commenti `-- changelog`.

## Compatibilità

- DSP target: `Sql160`. Non usare costrutti che richiedono `Sql170` senza
  approvazione esplicita.
- Niente feature deprecate (`*=`, `RAISERROR` con stringa formattata, hint
  `FASTFIRSTROW`).

## Riferimenti

- `docs/sql/security-rules.md`
- `docs/sql/performance-rules.md`
- `docs/sql/compliance.md`
- `src/Database/StaticCodeAnalysis.SuppressMessages.xml`
