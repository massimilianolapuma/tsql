CREATE TABLE pii.Address
(
    AddressId  INT            NOT NULL CONSTRAINT PK_pii_Address PRIMARY KEY,
    CustomerId INT            NOT NULL CONSTRAINT FK_pii_Address_Customer
                                  REFERENCES pii.Customer (CustomerId),
    Line1      NVARCHAR(200)  NOT NULL,
    City       NVARCHAR(100)  NOT NULL,
    Country    VARCHAR(2)     NOT NULL
);
GO
