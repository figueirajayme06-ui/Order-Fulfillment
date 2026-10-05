CREATE TABLE [dbo].[ViewRecipients] (
    [ViewId]             INT            NOT NULL,
    [RecipientLoginName] NVARCHAR (100) NOT NULL,
    CONSTRAINT [PK_ViewRecipients] PRIMARY KEY CLUSTERED ([ViewId] ASC, [RecipientLoginName] ASC),
    CONSTRAINT [FK_ViewRecipients_Views_ViewId] FOREIGN KEY ([ViewId]) REFERENCES [dbo].[Views] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ViewRecipients_Users_RecipientLoginName] FOREIGN KEY ([RecipientLoginName]) REFERENCES [dbo].[Users] ([LoginName]) ON DELETE CASCADE
);

GO
CREATE NONCLUSTERED INDEX [IX_ViewRecipients_RecipientLoginName]
    ON [dbo].[ViewRecipients]([RecipientLoginName] ASC, [ViewId] ASC);
