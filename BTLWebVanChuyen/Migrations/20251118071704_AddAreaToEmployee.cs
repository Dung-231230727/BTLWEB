using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTLWebVanChuyen.Migrations
{
    /// <inheritdoc />
    public partial class AddAreaToEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AreaId",
                table: "Employees",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_AreaId",
                table: "Employees",
                column: "AreaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Areas_AreaId",
                table: "Employees",
                column: "AreaId",
                principalTable: "Areas",
                principalColumn: "AreaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Areas_AreaId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_AreaId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "AreaId",
                table: "Employees");
        }
    }
}
