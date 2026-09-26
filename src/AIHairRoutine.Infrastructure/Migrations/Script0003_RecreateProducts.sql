-- Rich catalog for the 4-week hair schedule. The previous table held only seed data, so it is recreated.
-- Multi-valued fields are snake_case CSV tokens (the whole catalog is cached in memory and never filtered in SQL).
IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL
    DROP TABLE dbo.Products;

CREATE TABLE dbo.Products
(
    Id                          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Products_Id DEFAULT (NEWSEQUENTIALID())
                                                          CONSTRAINT PK_Products PRIMARY KEY,
    Name                        NVARCHAR(200)    NOT NULL,
    Brand                       NVARCHAR(120)    NULL,
    Description                 NVARCHAR(1000)   NULL,
    Category                    VARCHAR(20)      NOT NULL
        CONSTRAINT CK_Products_Category CHECK (Category IN
            ('shampoo', 'conditioner', 'mask', 'leave_in', 'oil', 'serum', 'finisher', 'treatment')),
    HairTypesCsv                VARCHAR(60)      NOT NULL CONSTRAINT DF_Products_HairTypes DEFAULT ('all'),
    TargetsCsv                  VARCHAR(200)     NOT NULL CONSTRAINT DF_Products_Targets DEFAULT (''),
    TreatmentTypesCsv           VARCHAR(60)      NOT NULL CONSTRAINT DF_Products_TreatmentTypes DEFAULT (''),
    SafeForChemical             BIT              NOT NULL CONSTRAINT DF_Products_SafeForChemical DEFAULT (0),
    ContraindicatedChemicalsCsv VARCHAR(120)     NOT NULL CONSTRAINT DF_Products_Contraindicated DEFAULT (''),
    UsageFrequency              VARCHAR(20)      NOT NULL
        CONSTRAINT CK_Products_UsageFrequency CHECK (UsageFrequency IN
            ('daily', 'twice_weekly', 'weekly', 'biweekly', 'monthly')),
    ActionTimeMinutes           INT              NULL
        CONSTRAINT CK_Products_ActionTime CHECK (ActionTimeMinutes IS NULL OR ActionTimeMinutes BETWEEN 1 AND 240),
    ApplicationOrder            INT              NOT NULL CONSTRAINT CK_Products_ApplicationOrder CHECK (ApplicationOrder >= 1),
    MinIntervalDays             INT              NOT NULL CONSTRAINT DF_Products_MinInterval DEFAULT (0)
                                                          CONSTRAINT CK_Products_MinInterval CHECK (MinIntervalDays >= 0),
    KeyIngredientsCsv           NVARCHAR(500)    NOT NULL CONSTRAINT DF_Products_KeyIngredients DEFAULT (''),
    AllergensCsv                VARCHAR(200)     NOT NULL CONSTRAINT DF_Products_Allergens DEFAULT (''),
    Price                       DECIMAL(10, 2)   NOT NULL CONSTRAINT CK_Products_Price CHECK (Price >= 0),
    SizeMl                      INT              NOT NULL CONSTRAINT CK_Products_SizeMl CHECK (SizeMl > 0),
    Active                      BIT              NOT NULL CONSTRAINT DF_Products_Active DEFAULT (1)
);

CREATE INDEX IX_Products_Active_Category ON dbo.Products (Active, Category);
