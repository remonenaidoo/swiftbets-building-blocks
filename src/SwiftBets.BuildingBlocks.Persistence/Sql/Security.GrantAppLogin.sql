IF DATABASE_PRINCIPAL_ID(N'swiftbets_app') IS NULL EXEC (N'CREATE ROLE swiftbets_app');
IF DATABASE_PRINCIPAL_ID(@Login) IS NULL
BEGIN
    DECLARE @create nvarchar(400) = N'CREATE USER ' + QUOTENAME(@Login) + N' FOR LOGIN ' + QUOTENAME(@Login);
    EXEC (@create);
END;
IF IS_ROLEMEMBER(N'swiftbets_app', @Login) = 0
BEGIN
    DECLARE @member nvarchar(400) = N'ALTER ROLE swiftbets_app ADD MEMBER ' + QUOTENAME(@Login);
    EXEC (@member);
END;
