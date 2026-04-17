/*
  Login local (docs/06 A1): hash de contraseña en AppUser.
  Ejecutar después de 001_schema_mvp.sql.
*/

IF COL_LENGTH('dbo.AppUser', 'PasswordHash') IS NULL
BEGIN
    ALTER TABLE dbo.AppUser ADD PasswordHash NVARCHAR(500) NULL;
END
GO
