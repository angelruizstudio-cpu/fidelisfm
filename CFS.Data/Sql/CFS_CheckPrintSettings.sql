IF OBJECT_ID('dbo.CFS_CheckPrintSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CFS_CheckPrintSettings
    (
        Id INT NOT NULL CONSTRAINT PK_CFS_CheckPrintSettings PRIMARY KEY,
        SheetOffsetX DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_SheetOffsetX DEFAULT (0),
        SheetOffsetY DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_SheetOffsetY DEFAULT (0),
        DateLeft DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_DateLeft DEFAULT (465),
        DateTop DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_DateTop DEFAULT (157),
        PayeeLeft DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_PayeeLeft DEFAULT (84),
        PayeeTop DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_PayeeTop DEFAULT (196),
        AmountLeft DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_AmountLeft DEFAULT (500),
        AmountTop DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_AmountTop DEFAULT (196),
        WordsLeft DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_WordsLeft DEFAULT (36),
        WordsTop DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_WordsTop DEFAULT (220),
        AddressLeft DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_AddressLeft DEFAULT (72),
        AddressTop DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_AddressTop DEFAULT (344),
        MemoLeft DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_MemoLeft DEFAULT (63),
        MemoTop DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_MemoTop DEFAULT (304),
        StubTitleLeft DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_StubTitleLeft DEFAULT (36),
        StubTitleTop DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_StubTitleTop DEFAULT (0),
        StubPayeeLeft DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_StubPayeeLeft DEFAULT (149),
        StubPayeeTop DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_StubPayeeTop DEFAULT (0),
        StubDateLeft DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_StubDateLeft DEFAULT (144),
        StubDateTop DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_StubDateTop DEFAULT (24),
        StubAccountLeft DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_StubAccountLeft DEFAULT (36),
        StubAccountTop DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_StubAccountTop DEFAULT (48),
        StubMemoLeft DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_StubMemoLeft DEFAULT (36),
        StubMemoTop DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_StubMemoTop DEFAULT (70),
        StubAmountLeft DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_StubAmountLeft DEFAULT (470),
        StubAmountTop DECIMAL(9,2) NOT NULL CONSTRAINT DF_CFS_CheckPrintSettings_StubAmountTop DEFAULT (0),
        UpdatedAt DATETIME2 NULL,
        UpdatedBy NVARCHAR(100) NULL
    );

    INSERT INTO dbo.CFS_CheckPrintSettings (Id)
    VALUES (1);
END
GO
