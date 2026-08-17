using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLaboratoriesAndPcIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IPAddress",
                table: "PCs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LaboratoryId",
                table: "PCs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MACAddress",
                table: "PCs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Laboratories",
                columns: table => new
                {
                    LaboratoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Laboratories", x => x.LaboratoryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PCs_LaboratoryId",
                table: "PCs",
                column: "LaboratoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_PCs_Laboratories_LaboratoryId",
                table: "PCs",
                column: "LaboratoryId",
                principalTable: "Laboratories",
                principalColumn: "LaboratoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PCs_Laboratories_LaboratoryId",
                table: "PCs");

            migrationBuilder.DropTable(
                name: "Laboratories");

            migrationBuilder.DropIndex(
                name: "IX_PCs_LaboratoryId",
                table: "PCs");

            migrationBuilder.DropColumn(
                name: "IPAddress",
                table: "PCs");

            migrationBuilder.DropColumn(
                name: "LaboratoryId",
                table: "PCs");

            migrationBuilder.DropColumn(
                name: "MACAddress",
                table: "PCs");
        }
    }
}
