CREATE TABLE dbo.CustomerOrder
(
    OrderId     INT            NOT NULL CONSTRAINT PK_dbo_CustomerOrder PRIMARY KEY,
    CustomerId  INT            NOT NULL,
    OrderDate   DATETIME2(3)   NOT NULL,
    TotalAmount DECIMAL(18, 2) NOT NULL
);
GO
