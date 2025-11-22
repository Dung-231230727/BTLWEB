using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTLWebVanChuyen.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShipmentBatchId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShipmentBatch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginWarehouseId = table.Column<int>(type: "int", nullable: false),
                    DestinationWarehouseId = table.Column<int>(type: "int", nullable: false),
                    ShipperId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentBatch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentBatch_Employees_ShipperId",
                        column: x => x.ShipperId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ShipmentBatch_Warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShipmentBatch_Warehouses_OriginWarehouseId",
                        column: x => x.OriginWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ShipmentBatchId",
                table: "Orders",
                column: "ShipmentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBatch_DestinationWarehouseId",
                table: "ShipmentBatch",
                column: "DestinationWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBatch_OriginWarehouseId",
                table: "ShipmentBatch",
                column: "OriginWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBatch_ShipperId",
                table: "ShipmentBatch",
                column: "ShipperId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_ShipmentBatch_ShipmentBatchId",
                table: "Orders",
                column: "ShipmentBatchId",
                principalTable: "ShipmentBatch",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_ShipmentBatch_ShipmentBatchId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "ShipmentBatch");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ShipmentBatchId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShipmentBatchId",
                table: "Orders");
        }
    }
}
