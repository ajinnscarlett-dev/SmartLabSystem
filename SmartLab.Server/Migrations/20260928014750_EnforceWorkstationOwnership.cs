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
            migrationBuilder.DropIndex(
                name: "IX_PCs_CurrentUserId",
                table: "PCs");

            migrationBuilder.CreateIndex(
                name: "IX_PCs_CurrentUserId",
                table: "PCs",
                column: "CurrentUserId",
                unique: true,
                filter: "[CurrentUserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PCs_CurrentUserId",
                table: "PCs");

            migrationBuilder.CreateIndex(
                name: "IX_PCs_CurrentUserId",
                table: "PCs",
                column: "CurrentUserId");
        }
    }
}
