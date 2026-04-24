# T-SQL — Compliance Contoso (GDPR / PII)

## Classificazione dati

Ogni colonna che contiene dati personali deve essere marcata con
`Extended Property` `Classification = 'PII'` o `'SENSITIVE'`.
Le tabelle nello schema `pii` sono **sempre** PII per definizione.

| Categoria      | Esempi                                | Schema/Tabella tipica |
|----------------|---------------------------------------|------------------------|
| PII diretta    | nome, cognome, email, telefono        | `pii.Customer`         |
| PII indiretta  | IP, device id, geolocation grezza     | `pii.Session`          |
| SENSITIVE      | dati sanitari, credenziali, payment   | `pii.PaymentMethod`    |

## Accesso

- Lettura PII consentita solo a ruoli `pii_reader_*`.
- Ogni SP che legge PII **deve** loggare l'accesso in `audit.AccessLog` via
  `audit.usp_LogAccess @SubjectId, @Caller, @Reason`.
- Il reviewer agentico deve segnalare SP che leggono `pii.*` senza scrivere
  in `audit.AccessLog`.

## Masking

- Output verso schemi `report.*` o viste pubbliche: applicare
  `Dynamic Data Masking` o funzioni `fn_MaskEmail`, `fn_MaskPhone`.
- Mai esporre PII grezza in viste senza filtro per ruolo.

## Retention

- Le tabelle PII devono avere colonna `RetentionUntilUtc DATETIME2(0) NOT NULL`.
- Job `audit.usp_PurgeExpired` esegue la cancellazione fisica oltre la
  retention.

## Right to be forgotten

- Implementato via `pii.usp_ForgetSubject @SubjectId`. Le nuove tabelle PII
  devono essere registrate in `pii.SubjectMap` per supportare la cancellazione
  a cascata.

## Cross-border

- Niente replica fisica di tabelle `pii.*` fuori dal tenant EU senza
  approvazione DPO.
