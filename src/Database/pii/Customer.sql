CREATE TABLE pii.Customer
(
    CustomerId       INT            NOT NULL CONSTRAINT PK_pii_Customer PRIMARY KEY,
    FullName         NVARCHAR(200)  NOT NULL,
    Email            NVARCHAR(256)  NULL,
    LastContactedUtc DATETIME2(3)   NULL
);
GO
