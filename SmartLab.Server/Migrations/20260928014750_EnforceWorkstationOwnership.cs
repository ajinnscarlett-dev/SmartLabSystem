using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class EnforceWorkstationOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_PCs_CurrentUserId'
                      AND object_id = OBJECT_ID(N'dbo.PCs'))
                BEGIN
                    DROP INDEX [IX_PCs_CurrentUserId] ON [dbo].[PCs];
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_PCs_CurrentUserId'
                      AND object_id = OBJECT_ID(N'dbo.PCs'))
                BEGIN
                    CREATE UNIQUE INDEX [IX_PCs_CurrentUserId]
                        ON [dbo].[PCs] ([CurrentUserId])
                        WHERE [CurrentUserId] IS NOT NULL;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_PCs_CurrentUserId'
                      AND object_id = OBJECT_ID(N'dbo.PCs'))
                BEGIN
                    DROP INDEX [IX_PCs_CurrentUserId] ON [dbo].[PCs];
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_PCs_CurrentUserId'
                      AND object_id = OBJECT_ID(N'dbo.PCs'))
                BEGIN
                    CREATE INDEX [IX_PCs_CurrentUserId]
                        ON [dbo].[PCs] ([CurrentUserId]);
                END;
                """);
        }
    }
}
