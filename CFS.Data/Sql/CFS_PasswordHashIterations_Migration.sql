/*
    Fidelis Financial Management - versioned PBKDF2 iteration count
    -----------------------------------------------------------------------------------------------
    Stores the PBKDF2 iteration count alongside each password hash so existing hashes keep verifying
    with the count they were created under (100000) while new/updated hashes use the current count
    (600000, OWASP). Existing passwords cannot be rehashed without the plaintext; they are upgraded
    lazily on the user's next successful login.

    - dbo.Usuarios.ContrasenaIteraciones: the iteration count for each user's stored password.
    - dbo.PendingSignups.PasswordIteraciones: the count used when the signup password was hashed, so
      provisioning copies the correct count into Usuarios (the hash is copied, never re-computed).

    Backfilled rows get 100000 (the only value used before this migration).
    Idempotent: safe to re-run.

    Run this against the PRODUCTION database BEFORE deploying the matching code — the write paths
    (create user, change/reset password, signup provisioning) set these columns explicitly.
*/

-- dbo.Usuarios
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'ContrasenaIteraciones'
)
BEGIN
    ALTER TABLE dbo.Usuarios ADD ContrasenaIteraciones INT NULL;
END
GO

UPDATE dbo.Usuarios
SET ContrasenaIteraciones = 100000
WHERE ContrasenaIteraciones IS NULL
  AND ContrasenaHash IS NOT NULL;
GO

-- dbo.PendingSignups
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.PendingSignups') AND name = 'PasswordIteraciones'
)
BEGIN
    ALTER TABLE dbo.PendingSignups ADD PasswordIteraciones INT NULL;
END
GO

UPDATE dbo.PendingSignups
SET PasswordIteraciones = 100000
WHERE PasswordIteraciones IS NULL
  AND PasswordHash IS NOT NULL;
GO
