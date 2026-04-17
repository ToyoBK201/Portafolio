/*
  Esquema MVP — Sistema de Solicitudes Tecnológicas Gubernamentales
  Motor: Microsoft SQL Server (2019+)
  Alineado a: docs/documento-formal-mvp-solicitudes-tech-gov.md, docs/03, docs/05
  
  Ejecución: aplicar en base nueva (crear BD antes o ajustar USE).
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- =============================================================================
-- Catálogos fijos (valores = contrato API / documento 03)
-- =============================================================================

CREATE TABLE dbo.RequestStatus (
    StatusId       TINYINT      NOT NULL,
    Code           VARCHAR(40)  NOT NULL,  -- p. ej. Draft, Submitted (serialización API)
    LabelEs        NVARCHAR(80) NOT NULL,
    IsTerminal     BIT          NOT NULL CONSTRAINT DF_RequestStatus_IsTerminal DEFAULT (0),
    SortOrder      TINYINT      NOT NULL,
    CONSTRAINT PK_RequestStatus PRIMARY KEY (StatusId),
    CONSTRAINT UQ_RequestStatus_Code UNIQUE (Code)
);

INSERT INTO dbo.RequestStatus (StatusId, Code, LabelEs, IsTerminal, SortOrder) VALUES
(1,  'Draft',                      N'Borrador',                         0, 1),
(2,  'Submitted',                  N'Enviada',                          0, 2),
(3,  'InTicAnalysis',              N'En análisis TIC',                  0, 3),
(4,  'PendingApproval',            N'Pendiente aprobación',             0, 4),
(5,  'Approved',                   N'Aprobada',                         0, 5),
(6,  'Rejected',                   N'Rechazada',                        1, 6),
(7,  'InProgress',                 N'En ejecución',                     0, 7),
(8,  'PendingRequesterValidation', N'Pendiente validación solicitante', 0, 8),
(9,  'Closed',                     N'Cerrada',                          1, 9),
(10, 'Cancelled',                  N'Cancelada',                        1, 10);

CREATE TABLE dbo.RequestType (
    RequestTypeId TINYINT      NOT NULL,
    Code          VARCHAR(64)  NOT NULL,
    LabelEs       NVARCHAR(120) NOT NULL,
    SortOrder     TINYINT      NOT NULL,
    CONSTRAINT PK_RequestType PRIMARY KEY (RequestTypeId),
    CONSTRAINT UQ_RequestType_Code UNIQUE (Code)
);

INSERT INTO dbo.RequestType (RequestTypeId, Code, LabelEs, SortOrder) VALUES
(1, 'HardwareAcquisition',        N'Adquisición de hardware',              1),
(2, 'SoftwareLicensing',          N'Adquisición o licenciamiento software', 2),
(3, 'SystemDevelopment',          N'Desarrollo o evolutivo de sistema',    3),
(4, 'InfrastructureConnectivity', N'Infraestructura y conectividad',      4),
(5, 'MajorTechnicalSupport',      N'Soporte técnico / incidente mayor',    5),
(6, 'InformationSecurity',       N'Seguridad de la información',         6),
(7, 'DataInteroperability',       N'Datos / interoperabilidad',           7);

CREATE TABLE dbo.Priority (
    PriorityId TINYINT     NOT NULL,
    Code       VARCHAR(20) NOT NULL,
    LabelEs    NVARCHAR(20) NOT NULL,
    SortOrder  TINYINT     NOT NULL,
    CONSTRAINT PK_Priority PRIMARY KEY (PriorityId),
    CONSTRAINT UQ_Priority_Code UNIQUE (Code)
);

INSERT INTO dbo.Priority (PriorityId, Code, LabelEs, SortOrder) VALUES
(1, 'Low',      N'Baja',    1),
(2, 'Medium',   N'Media',   2),
(3, 'High',     N'Alta',   3),
(4, 'Critical', N'Crítica', 4);

CREATE TABLE dbo.AppRole (
    RoleId    TINYINT      NOT NULL,
    Code      VARCHAR(64)  NOT NULL,
    LabelEs   NVARCHAR(80) NOT NULL,
    SortOrder TINYINT      NOT NULL,
    CONSTRAINT PK_AppRole PRIMARY KEY (RoleId),
    CONSTRAINT UQ_AppRole_Code UNIQUE (Code)
);

INSERT INTO dbo.AppRole (RoleId, Code, LabelEs, SortOrder) VALUES
(1, 'Requester',             N'Solicitante',                1),
(2, 'AreaCoordinator',       N'Coordinador de área',        2),
(3, 'TicAnalyst',            N'Analista TIC',               3),
(4, 'InstitutionalApprover', N'Aprobador institucional',   4),
(5, 'Implementer',            N'Implementador',              5),
(6, 'SystemAdministrator',    N'Administrador del sistema', 6),
(7, 'Auditor',                N'Auditor',                    7);

-- =============================================================================
-- Unidades organizativas (catálogo maestro editable por Admin)
-- =============================================================================

CREATE TABLE dbo.OrganizationalUnit (
    UnitId      INT            NOT NULL IDENTITY(1, 1),
    Code        NVARCHAR(32)   NOT NULL,
    Name        NVARCHAR(200)  NOT NULL,
    IsActive    BIT            NOT NULL CONSTRAINT DF_OrgUnit_IsActive DEFAULT (1),
    CreatedAtUtc DATETIME2(7)  NOT NULL CONSTRAINT DF_OrgUnit_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_OrganizationalUnit PRIMARY KEY (UnitId),
    CONSTRAINT UQ_OrganizationalUnit_Code UNIQUE (Code)
);

CREATE INDEX IX_OrganizationalUnit_IsActive ON dbo.OrganizationalUnit (IsActive) WHERE IsActive = 1;

-- =============================================================================
-- Usuarios y asignación de roles (RBAC)
-- =============================================================================

CREATE TABLE dbo.AppUser (
    UserId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_AppUser_UserId DEFAULT (NEWSEQUENTIALID()),
    Email            NVARCHAR(320)    NOT NULL,
    DisplayName      NVARCHAR(200)    NOT NULL,
    ExternalSubjectId NVARCHAR(450)    NULL,
    IsActive         BIT              NOT NULL CONSTRAINT DF_AppUser_IsActive DEFAULT (1),
    CreatedAtUtc     DATETIME2(7)     NOT NULL CONSTRAINT DF_AppUser_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_AppUser PRIMARY KEY (UserId),
    CONSTRAINT UQ_AppUser_Email UNIQUE (Email)
);

CREATE UNIQUE INDEX IX_AppUser_ExternalSubjectId
    ON dbo.AppUser (ExternalSubjectId)
    WHERE ExternalSubjectId IS NOT NULL;

CREATE TABLE dbo.UserRole (
    UserRoleId           BIGINT           NOT NULL IDENTITY(1, 1),
    UserId               UNIQUEIDENTIFIER NOT NULL,
    RoleId               TINYINT          NOT NULL,
    OrganizationalUnitId INT              NULL,
    AssignedAtUtc        DATETIME2(7)     NOT NULL CONSTRAINT DF_UserRole_Assigned DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_UserRole PRIMARY KEY (UserRoleId),
    CONSTRAINT FK_UserRole_User     FOREIGN KEY (UserId) REFERENCES dbo.AppUser (UserId),
    CONSTRAINT FK_UserRole_Role     FOREIGN KEY (RoleId) REFERENCES dbo.AppRole (RoleId),
    CONSTRAINT FK_UserRole_OrgUnit  FOREIGN KEY (OrganizationalUnitId) REFERENCES dbo.OrganizationalUnit (UnitId)
);

-- Unicidad: una fila por (usuario, rol) cuando no hay ámbito de unidad; otra por (usuario, rol, unidad) cuando aplica.
CREATE UNIQUE INDEX UX_UserRole_User_Role_SansUnit
    ON dbo.UserRole (UserId, RoleId)
    WHERE OrganizationalUnitId IS NULL;

CREATE UNIQUE INDEX UX_UserRole_User_Role_WithUnit
    ON dbo.UserRole (UserId, RoleId, OrganizationalUnitId)
    WHERE OrganizationalUnitId IS NOT NULL;

-- =============================================================================
-- Parámetros de sistema (límites adjuntos, etc.)
-- =============================================================================

CREATE TABLE dbo.SystemParameter (
    ParameterKey   NVARCHAR(64)  NOT NULL,
    ParameterValue NVARCHAR(MAX) NOT NULL,
    UpdatedAtUtc   DATETIME2(7)  NOT NULL CONSTRAINT DF_SysParam_Updated DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_SystemParameter PRIMARY KEY (ParameterKey)
);

INSERT INTO dbo.SystemParameter (ParameterKey, ParameterValue) VALUES
(N'Attachment.MaxBytesPerFile', N'10485760'),
(N'Attachment.MaxFilesPerRequest', N'10'),
(N'Attachment.AllowedExtensions', N'pdf,png,jpg,jpeg,docx,xlsx,csv,zip');

-- =============================================================================
-- Solicitudes
-- =============================================================================

CREATE TABLE dbo.Request (
    RequestId                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Request_Id DEFAULT (NEWSEQUENTIALID()),
    Folio                        NVARCHAR(40)     NULL,
    Title                        NVARCHAR(200)    NOT NULL,
    Description                  NVARCHAR(MAX)    NOT NULL,
    BusinessJustification        NVARCHAR(MAX)    NOT NULL,
    RequestTypeId                TINYINT          NOT NULL,
    PriorityId                   TINYINT          NOT NULL,
    RequestingUnitId             INT              NOT NULL,
    RequesterUserId              UNIQUEIDENTIFIER NOT NULL,
    StatusId                     TINYINT          NOT NULL CONSTRAINT DF_Request_Status DEFAULT (1),
    DesiredDate                  DATE             NULL,
    SpecificPayloadJson          NVARCHAR(MAX)    NULL,
    AssignedAnalystUserId        UNIQUEIDENTIFIER NULL,
    AssignedImplementerUserId    UNIQUEIDENTIFIER NULL,
    CreatedAtUtc                 DATETIME2(7)     NOT NULL CONSTRAINT DF_Request_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc                 DATETIME2(7)     NOT NULL CONSTRAINT DF_Request_Updated DEFAULT (SYSUTCDATETIME()),
    SubmittedAtUtc               DATETIME2(7)     NULL,
    RowVersion                   ROWVERSION       NOT NULL,
    CONSTRAINT PK_Request PRIMARY KEY (RequestId),
    CONSTRAINT FK_Request_Type          FOREIGN KEY (RequestTypeId)     REFERENCES dbo.RequestType (RequestTypeId),
    CONSTRAINT FK_Request_Priority      FOREIGN KEY (PriorityId)       REFERENCES dbo.Priority (PriorityId),
    CONSTRAINT FK_Request_OrgUnit     FOREIGN KEY (RequestingUnitId) REFERENCES dbo.OrganizationalUnit (UnitId),
    CONSTRAINT FK_Request_Requester   FOREIGN KEY (RequesterUserId)   REFERENCES dbo.AppUser (UserId),
    CONSTRAINT FK_Request_Status      FOREIGN KEY (StatusId)       REFERENCES dbo.RequestStatus (StatusId),
    CONSTRAINT FK_Request_Analist     FOREIGN KEY (AssignedAnalystUserId)     REFERENCES dbo.AppUser (UserId),
    CONSTRAINT FK_Request_Implementer FOREIGN KEY (AssignedImplementerUserId) REFERENCES dbo.AppUser (UserId)
);

CREATE UNIQUE INDEX UX_Request_Folio ON dbo.Request (Folio) WHERE Folio IS NOT NULL;

CREATE INDEX IX_Request_Status_Updated ON dbo.Request (StatusId, UpdatedAtUtc DESC);
CREATE INDEX IX_Request_Requester ON dbo.Request (RequesterUserId, UpdatedAtUtc DESC);
CREATE INDEX IX_Request_Unit ON dbo.Request (RequestingUnitId, StatusId);
CREATE INDEX IX_Request_Analist ON dbo.Request (AssignedAnalystUserId) WHERE AssignedAnalystUserId IS NOT NULL;
CREATE INDEX IX_Request_Implementer ON dbo.Request (AssignedImplementerUserId) WHERE AssignedImplementerUserId IS NOT NULL;

-- =============================================================================
-- Adjuntos (binarios fuera de BD; metadatos aquí)
-- =============================================================================

CREATE TABLE dbo.RequestAttachment (
    AttachmentId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Attachment_Id DEFAULT (NEWSEQUENTIALID()),
    RequestId      UNIQUEIDENTIFIER NOT NULL,
    FileName       NVARCHAR(260)    NOT NULL,
    ContentType    NVARCHAR(200)    NOT NULL,
    SizeBytes      BIGINT           NOT NULL,
    StoragePath    NVARCHAR(1024)   NOT NULL,
    UploadedByUserId UNIQUEIDENTIFIER NOT NULL,
    UploadedAtUtc  DATETIME2(7)     NOT NULL CONSTRAINT DF_Attachment_Uploaded DEFAULT (SYSUTCDATETIME()),
    IsDeleted      BIT              NOT NULL CONSTRAINT DF_Attachment_IsDeleted DEFAULT (0),
    DeletedAtUtc   DATETIME2(7)     NULL,
    DeletedByUserId UNIQUEIDENTIFIER NULL,
    ReplacesAttachmentId UNIQUEIDENTIFIER NULL,
    CONSTRAINT PK_RequestAttachment PRIMARY KEY (AttachmentId),
    CONSTRAINT FK_Attachment_Request FOREIGN KEY (RequestId) REFERENCES dbo.Request (RequestId),
    CONSTRAINT FK_Attachment_Uploader FOREIGN KEY (UploadedByUserId) REFERENCES dbo.AppUser (UserId),
    CONSTRAINT FK_Attachment_DeletedBy FOREIGN KEY (DeletedByUserId) REFERENCES dbo.AppUser (UserId),
    CONSTRAINT FK_Attachment_Replaces   FOREIGN KEY (ReplacesAttachmentId) REFERENCES dbo.RequestAttachment (AttachmentId)
);

CREATE INDEX IX_Attachment_Request ON dbo.RequestAttachment (RequestId) WHERE IsDeleted = 0;

-- =============================================================================
-- Comentarios
-- =============================================================================

CREATE TABLE dbo.RequestComment (
    CommentId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Comment_Id DEFAULT (NEWSEQUENTIALID()),
    RequestId      UNIQUEIDENTIFIER NOT NULL,
    AuthorUserId   UNIQUEIDENTIFIER NOT NULL,
    Body           NVARCHAR(MAX)    NOT NULL,
    IsInternal     BIT              NOT NULL CONSTRAINT DF_Comment_Internal DEFAULT (0),
    CreatedAtUtc   DATETIME2(7)     NOT NULL CONSTRAINT DF_Comment_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_RequestComment PRIMARY KEY (CommentId),
    CONSTRAINT FK_Comment_Request FOREIGN KEY (RequestId) REFERENCES dbo.Request (RequestId),
    CONSTRAINT FK_Comment_Author  FOREIGN KEY (AuthorUserId) REFERENCES dbo.AppUser (UserId)
);

CREATE INDEX IX_Comment_Request ON dbo.RequestComment (RequestId, CreatedAtUtc);

-- =============================================================================
-- Auditoría (append-only; docs/05)
-- =============================================================================

CREATE TABLE dbo.AuditLog (
    AuditId         BIGINT           NOT NULL IDENTITY(1, 1),
    OccurredAtUtc   DATETIME2(7)     NOT NULL CONSTRAINT DF_Audit_Occurred DEFAULT (SYSUTCDATETIME()),
    CorrelationId   UNIQUEIDENTIFIER NULL,
    ActorUserId     UNIQUEIDENTIFIER NOT NULL,
    ActorRole       NVARCHAR(64)     NOT NULL,
    Action          NVARCHAR(64)     NOT NULL,
    EntityType      NVARCHAR(64)     NOT NULL,
    EntityId        NVARCHAR(64)     NOT NULL,
    RequestId       UNIQUEIDENTIFIER NULL,
    FromStatusId    TINYINT          NULL,
    ToStatusId      TINYINT          NULL,
    ClientIp        VARBINARY(16)    NULL,
    UserAgent       NVARCHAR(256)    NULL,
    PayloadSummary  NVARCHAR(MAX)    NULL,
    PayloadDiff     NVARCHAR(MAX)    NULL,
    Success         BIT              NOT NULL CONSTRAINT DF_Audit_Success DEFAULT (1),
    CONSTRAINT PK_AuditLog PRIMARY KEY (AuditId),
    CONSTRAINT FK_Audit_Request FOREIGN KEY (RequestId) REFERENCES dbo.Request (RequestId),
    CONSTRAINT FK_Audit_FromStatus FOREIGN KEY (FromStatusId) REFERENCES dbo.RequestStatus (StatusId),
    CONSTRAINT FK_Audit_ToStatus   FOREIGN KEY (ToStatusId) REFERENCES dbo.RequestStatus (StatusId)
);

CREATE INDEX IX_Audit_Request_Time ON dbo.AuditLog (RequestId, OccurredAtUtc DESC);
CREATE INDEX IX_Audit_Actor_Time ON dbo.AuditLog (ActorUserId, OccurredAtUtc DESC);
CREATE INDEX IX_Audit_Action_Time ON dbo.AuditLog (Action, OccurredAtUtc DESC);

GO
