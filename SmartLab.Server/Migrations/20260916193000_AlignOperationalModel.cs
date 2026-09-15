using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AlignOperationalModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeacherLaboratoryAuthorizations_Laboratories_LaboratoryId",
                table: "TeacherLaboratoryAuthorizations");

            migrationBuilder.DropForeignKey(
                name: "FK_TeacherLaboratoryAuthorizations_Users_TeacherUserId",
                table: "TeacherLaboratoryAuthorizations");

            migrationBuilder.DropIndex(
                name: "IX_PCs_LaboratoryId",
                table: "PCs");

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

            migrationBuilder.CreateIndex(
                name: "IX_TeacherLaboratoryAuthorizations_LaboratoryId",
                table: "TeacherLaboratoryAuthorizations",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherLaboratoryAuthorizations_TeacherUserId",
                table: "TeacherLaboratoryAuthorizations",
                column: "TeacherUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PcUsageHistory_UserId",
                table: "PcUsageHistory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_TechnicianUserId",
                table: "MaintenanceRecords",
                column: "TechnicianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistanceRequests_ResolvedByUserId",
                table: "AssistanceRequests",
                column: "ResolvedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherLaboratoryAuthorizations_Laboratories_LaboratoryId",
                table: "TeacherLaboratoryAuthorizations",
                column: "LaboratoryId",
                principalTable: "Laboratories",
                principalColumn: "LaboratoryId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherLaboratoryAuthorizations_Users_TeacherUserId",
                table: "TeacherLaboratoryAuthorizations",
                column: "TeacherUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeacherLaboratoryAuthorizations_Laboratories_LaboratoryId",
                table: "TeacherLaboratoryAuthorizations");

            migrationBuilder.DropForeignKey(
                name: "FK_TeacherLaboratoryAuthorizations_Users_TeacherUserId",
                table: "TeacherLaboratoryAuthorizations");

            migrationBuilder.DropIndex(
                name: "IX_TeacherLaboratoryAuthorizations_LaboratoryId",
                table: "TeacherLaboratoryAuthorizations");

            migrationBuilder.DropIndex(
                name: "IX_TeacherLaboratoryAuthorizations_TeacherUserId",
                table: "TeacherLaboratoryAuthorizations");

            migrationBuilder.DropIndex(
                name: "IX_PcUsageHistory_UserId",
                table: "PcUsageHistory");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRecords_TechnicianUserId",
                table: "MaintenanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AssistanceRequests_ResolvedByUserId",
                table: "AssistanceRequests");

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

            migrationBuilder.CreateIndex(
                name: "IX_PCs_LaboratoryId",
                table: "PCs",
                column: "LaboratoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherLaboratoryAuthorizations_Laboratories_LaboratoryId",
                table: "TeacherLaboratoryAuthorizations",
                column: "LaboratoryId",
                principalTable: "Laboratories",
                principalColumn: "LaboratoryId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherLaboratoryAuthorizations_Users_TeacherUserId",
                table: "TeacherLaboratoryAuthorizations",
                column: "TeacherUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
