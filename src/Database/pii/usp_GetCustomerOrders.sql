CREATE PROCEDURE pii.usp_GetCustomerOrders
    @CustomerId      INT,
    @SortColumn      SYSNAME = N'OrderDate',
    @MarkAsContacted BIT     = 0
AS
BEGIN
    -- NOTA: SP che legge dati PII (pii.Customer + pii.Address) e, se richiesto,
    -- aggiorna due tabelle in sequenza. Smoke-test deliberatamente difettoso.

    DECLARE @sql NVARCHAR(MAX);

    -- 1) Lettura PII senza audit log: nessuna chiamata a audit.usp_LogAccess.
    SELECT *
    FROM   pii.Customer  AS c
    JOIN   pii.Address   AS a ON a.CustomerId = c.CustomerId
    WHERE  c.CustomerId = @CustomerId;

    -- 2) Dynamic SQL: la colonna di ordinamento arriva dal chiamante e viene
    --    concatenata in sp_executesql senza QUOTENAME. CT0001 non lo intercetta
    --    perché il branch è parametrizzato, ma resta un vettore di SQL injection.
    SET @sql = N'SELECT OrderId, OrderDate, TotalAmount
                 FROM dbo.CustomerOrder
                 WHERE CustomerId = @cid
                 ORDER BY ' + @SortColumn + N' DESC';

    EXEC sp_executesql
        @sql,
        N'@cid INT',
        @cid = @CustomerId;

    -- 3) Aggiornamento multi-step senza transazione: se la INSERT in audit.Contact
    --    fallisce, pii.Customer resta marcato come contattato => stato incoerente.
    IF @MarkAsContacted = 1
    BEGIN
        UPDATE pii.Customer
        SET    LastContactedUtc = SYSUTCDATETIME()
        WHERE  CustomerId = @CustomerId;

        INSERT INTO audit.Contact (CustomerId, ContactedUtc, Channel)
        VALUES (@CustomerId, SYSUTCDATETIME(), N'sp');
    END
END;
GO

-- 4) Permesso troppo ampio: GRANT EXECUTE a PUBLIC su una SP che tocca PII.
GRANT EXECUTE ON OBJECT::pii.usp_GetCustomerOrders TO PUBLIC;
GO
