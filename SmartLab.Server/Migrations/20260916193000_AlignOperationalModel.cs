using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLab.Server.Migrations
{
    public partial class AlignOperationalModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration reconciles older databases that may have been created
            // by the runtime EnsureAuthorizationTableAsync helper before EF owned
            // the schema. All reconciliation operations are guarded so the same
            // migration is safe on fresh and previously initialized databases.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.TeacherLaboratoryAuthorizations', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.TeacherLaboratoryAuthorizations
                    (
                        TeacherLaboratoryAuthorizationId INT IDENTITY(1,1) NOT NULL
                            CONSTRAINT PK_TeacherLaboratoryAuthorizations PRIMARY KEY,
                        TeacherUserId INT NOT NULL,
                        LaboratoryId INT NOT NULL,
                        CreatedAt DATETIME2 NOT NULL
                            CONSTRAINT DF_TeacherLaboratoryAuthorizations_CreatedAt DEFAULT(GETDATE()),
                        CONSTRAINT UQ_TeacherLaboratoryAuthorizations UNIQUE(TeacherUserId, LaboratoryId),
                        CONSTRAINT FK_TeacherLaboratoryAuthorizations_Users_TeacherUserId
                            FOREIGN KEY (TeacherUserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE,
                        CONSTRAINT FK_TeacherLaboratoryAuthorizations_Laboratories_LaboratoryId
                            FOREIGN KEY (LaboratoryId) REFERENCES dbo.Laboratories(LaboratoryId) ON DELETE CASCADE
                    );
                END;
                """);

            // Normalize any pre-existing authorization foreign keys regardless of
            // their historical names. Only this table and these two relationships
            // are touched.
            migrationBuilder.Sql("""
                DECLARE @sql nvarchar(max) = N'';

                SELECT @sql = @sql +
                    N'ALTER TABLE dbo.TeacherLaboratoryAuthorizations DROP CONSTRAINT ' +
                    QUOTENAME(fk.name) + N';'
                FROM sys.foreign_keys fk
                INNER JOIN sys.foreign_key_columns fkc
                    ON fk.object_id = fkc.constraint_object_id
                INNER JOIN sys.tables t
                    ON fk.parent_object_id = t.object_id
                INNER JOIN sys.columns c
                    ON c.object_id = t.object_id
                   AND c.column_id = fkc.parent_column_id
                WHERE t.name = N'TeacherLaboratoryAuthorizations'
                  AND c.name IN (N'TeacherUserId', N'LaboratoryId');

                IF @sql <> N''
                    EXEC sp_executesql @sql;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_PCs_LaboratoryId'
                      AND object_id = OBJECT_ID(N'dbo.PCs')
                )
                BEGIN
                    DROP INDEX IX_PCs_LaboratoryId ON dbo.PCs;
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PCs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PCNumber",
                table: "PCs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "MACAddress",
                table: "PCs",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_TeacherLaboratoryAuthorizations_LaboratoryId'
                      AND object_id = OBJECT_ID(N'dbo.TeacherLaboratoryAuthorizations')
                )
                CREATE INDEX IX_TeacherLaboratoryAuthorizations_LaboratoryId
                    ON dbo.TeacherLaboratoryAuthorizations(LaboratoryId);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_TeacherLaboratoryAuthorizations_TeacherUserId'
                      AND object_id = OBJECT_ID(N'dbo.TeacherLaboratoryAuthorizations')
                )
                CREATE INDEX IX_TeacherLaboratoryAuthorizations_TeacherUserId
                    ON dbo.TeacherLaboratoryAuthorizations(TeacherUserId);
                """);

            // These indexes were already introduced by CompleteOperationalData on
            // normal migration paths. Create them only when a legacy database is
            // missing them, preventing duplicate-index failures.
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_PcUsageHistory_UserId'
                      AND object_id = OBJECT_ID(N'dbo.PcUsageHistory')
                )
                CREATE INDEX IX_PcUsageHistory_UserId
                    ON dbo.PcUsageHistory(UserId);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_MaintenanceRecords_TechnicianUserId'
                      AND object_id = OBJECT_ID(N'dbo.MaintenanceRecords')
                )
                CREATE INDEX IX_MaintenanceRecords_TechnicianUserId
                    ON dbo.MaintenanceRecords(TechnicianUserId);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_AssistanceRequests_ResolvedByUserId'
                      AND object_id = OBJECT_ID(N'dbo.AssistanceRequests')
                )
                CREATE INDEX IX_AssistanceRequests_ResolvedByUserId
                    ON dbo.AssistanceRequests(ResolvedByUserId);
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TeacherLaboratoryAuthorizations_Laboratories_LaboratoryId'
                      AND parent_object_id = OBJECT_ID(N'dbo.TeacherLaboratoryAuthorizations')
                )
                ALTER TABLE dbo.TeacherLaboratoryAuthorizations
                    ADD CONSTRAINT FK_TeacherLaboratoryAuthorizations_Laboratories_LaboratoryId
                    FOREIGN KEY (LaboratoryId)
                    REFERENCES dbo.Laboratories(LaboratoryId)
                    ON DELETE CASCADE;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TeacherLaboratoryAuthorizations_Users_TeacherUserId'
                      AND parent_object_id = OBJECT_ID(N'dbo.TeacherLaboratoryAuthorizations')
                )
                ALTER TABLE dbo.TeacherLaboratoryAuthorizations
                    ADD CONSTRAINT FK_TeacherLaboratoryAuthorizations_Users_TeacherUserId
                    FOREIGN KEY (TeacherUserId)
                    REFERENCES dbo.Users(UserId)
                    ON DELETE CASCADE;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = N'FK_TeacherLaboratoryAuthorizations_Laboratories_LaboratoryId'
                      AND parent_object_id = OBJECT_ID(N'dbo.TeacherLaboratoryAuthorizations')
                )
                ALTER TABLE dbo.TeacherLaboratoryAuthorizations
                    DROP CONSTRAINT FK_TeacherLaboratoryAuthorizations_Laboratories_LaboratoryId;

                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = N'FK_TeacherLaboratoryAuthorizations_Users_TeacherUserId'
                      AND parent_object_id = OBJECT_ID(N'dbo.TeacherLaboratoryAuthorizations')
                )
                ALTER TABLE dbo.TeacherLaboratoryAuthorizations
                    DROP CONSTRAINT FK_TeacherLaboratoryAuthorizations_Users_TeacherUserId;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_TeacherLaboratoryAuthorizations_LaboratoryId'
                      AND object_id = OBJECT_ID(N'dbo.TeacherLaboratoryAuthorizations')
                )
                DROP INDEX IX_TeacherLaboratoryAuthorizations_LaboratoryId
                    ON dbo.TeacherLaboratoryAuthorizations;

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_TeacherLaboratoryAuthorizations_TeacherUserId'
                      AND object_id = OBJECT_ID(N'dbo.TeacherLaboratoryAuthorizations')
                )
                DROP INDEX IX_TeacherLaboratoryAuthorizations_TeacherUserId
                    ON dbo.TeacherLaboratoryAuthorizations;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_PCs_LaboratoryId'
                      AND object_id = OBJECT_ID(N'dbo.PCs')
                )
                CREATE INDEX IX_PCs_LaboratoryId ON dbo.PCs(LaboratoryId);
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PCs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "PCNumber",
                table: "PCs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "MACAddress",
                table: "PCs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
