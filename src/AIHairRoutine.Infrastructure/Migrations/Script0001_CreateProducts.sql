IF OBJECT_ID('dbo.Products', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
        Name         NVARCHAR(200)     NOT NULL,
        Brand        NVARCHAR(120)     NULL,
        Category     VARCHAR(40)       NOT NULL,
        TargetsCsv   VARCHAR(300)      NOT NULL,
        HairTypesCsv VARCHAR(120)      NOT NULL,
        ForChemical  BIT               NOT NULL CONSTRAINT DF_Products_ForChemical DEFAULT (0),
        Description  NVARCHAR(1000)    NULL,
        Active       BIT               NOT NULL CONSTRAINT DF_Products_Active DEFAULT (1)
    );

    CREATE INDEX IX_Products_Active ON dbo.Products (Active);
END
