/*
  Notificaciones in-app (docs/01 §2.2)
  Aplicar sobre base ya creada con 001_schema_mvp.sql
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.UserNotification', N'U') IS NOT NULL
BEGIN
    RAISERROR(N'Tabla dbo.UserNotification ya existe.', 16, 1);
    RETURN;
END

CREATE TABLE dbo.UserNotification (
    NotificationId UNIQUEIDENTIFIER NOT NULL,
    UserId         UNIQUEIDENTIFIER NOT NULL,
    RequestId      UNIQUEIDENTIFIER NULL,
    Title          NVARCHAR(200)   NOT NULL,
    [Message]      NVARCHAR(1000)  NOT NULL,
    Category       VARCHAR(64)     NOT NULL CONSTRAINT DF_UserNotification_Category DEFAULT ('RequestTransition'),
    ReadAtUtc      DATETIME2(7)    NULL,
    CreatedAtUtc   DATETIME2(7)    NOT NULL CONSTRAINT DF_UserNotification_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_UserNotification PRIMARY KEY (NotificationId),
    CONSTRAINT FK_UserNotification_User    FOREIGN KEY (UserId) REFERENCES dbo.AppUser (UserId),
    CONSTRAINT FK_UserNotification_Request FOREIGN KEY (RequestId) REFERENCES dbo.Request (RequestId)
);

CREATE INDEX IX_UserNotification_User_Created ON dbo.UserNotification (UserId, CreatedAtUtc DESC);
CREATE INDEX IX_UserNotification_Unread ON dbo.UserNotification (UserId, CreatedAtUtc DESC) WHERE ReadAtUtc IS NULL;
