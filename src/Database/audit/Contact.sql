CREATE TABLE audit.Contact
(
    ContactId    BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_audit_Contact PRIMARY KEY,
    CustomerId   INT           NOT NULL,
    ContactedUtc DATETIME2(3)  NOT NULL,
    Channel      NVARCHAR(20)  NOT NULL
);
GO
