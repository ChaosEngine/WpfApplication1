CREATE TABLE [dbo].[accounts] (
    [user_id]      INT           CONSTRAINT [DF_accounts_user_id] DEFAULT ((0)) NOT NULL,
    [user_name]    VARCHAR (100) NULL,
    [clear_passwd] VARCHAR (100) NULL,
    [passwd]       VARCHAR (100) NULL,
    [dummy]        VARCHAR (100) NULL,
    CONSTRAINT [PK_accounts] PRIMARY KEY CLUSTERED ([user_id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_accounts_clear_passwd]
    ON [dbo].[accounts]([clear_passwd] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_accounts_dummy]
    ON [dbo].[accounts]([dummy] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_accounts_passwd]
    ON [dbo].[accounts]([passwd] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_accounts_user_name]
    ON [dbo].[accounts]([user_name] ASC);

