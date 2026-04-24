# T-SQL — Regole di performance Contoso

Linee guida per il reviewer agentico. Le regole deterministiche sono coperte
da `SR*` (DacFx); qui i temi semantici.

## Sargability

- Niente funzioni sulle colonne in `WHERE` / `JOIN`:
  - `WHERE YEAR(OrderDate) = 2025` ❌ → `WHERE OrderDate >= '20250101' AND OrderDate < '20260101'` ✅
  - `WHERE UPPER(Email) = @e` ❌ → confrontare con collation case-insensitive.
- Niente cast impliciti su colonne indicizzate (es. `WHERE NvarcharCol = @varcharParam`).

## Set-based vs cursor

- Cursori vietati salvo casi documentati (DDL massivo, integrazione legacy).
  Se necessario, usare `LOCAL FAST_FORWARD READ_ONLY`.
- Loop `WHILE` su tabelle = code smell: il reviewer deve proporre
  riscrittura set-based.

## SELECT *

- Vietato in produzione. Tollerato solo in CTE di staging temporaneo
  immediatamente proiettato.

## Indici

- Ogni nuova FK deve avere indice di copertura sul lato child.
- Indici filtered preferiti per colonne con alta selettività su un sottoinsieme
  (es. `WHERE Status = 'Active'`).
- Mai più di 5 indici nonclustered su tabelle OLTP ad alta scrittura senza
  giustificazione.

## NOLOCK / Isolation

- `WITH (NOLOCK)` consentito solo su query di reportistica esplicitamente
  marcate. Mai su SP transazionali.
- Default isolation: `READ COMMITTED SNAPSHOT`. Cambiare livello richiede
  motivazione nel commit message.

## Tempdb

- Preferire table variables per set < 1000 righe; `#temp` per dataset più
  grandi (statistiche).
- Niente `SELECT INTO #t` su query lunghe in transazione (lock su sysobjects).
