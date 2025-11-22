using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTLWebVanChuyen.Migrations
{
    /// <inheritdoc />
    public partial class V : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_ShipmentBatch_ShipmentBatchId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentBatch_Employees_ShipperId",
                table: "ShipmentBatch");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentBatch_Warehouses_DestinationWarehouseId",
                table: "ShipmentBatch");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentBatch_Warehouses_OriginWarehouseId",
                table: "ShipmentBatch");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ShipmentBatch",
                table: "ShipmentBatch");

            migrationBuilder.RenameTable(
                name: "ShipmentBatch",
                newName: "ShipmentBatches");

            migrationBuilder.RenameIndex(
                name: "IX_ShipmentBatch_ShipperId",
                table: "ShipmentBatches",
                newName: "IX_ShipmentBatches_ShipperId");

            migrationBuilder.RenameIndex(
                name: "IX_ShipmentBatch_OriginWarehouseId",
                table: "ShipmentBatches",
                newName: "IX_ShipmentBatches_OriginWarehouseId");

            migrationBuilder.RenameIndex(
                name: "IX_ShipmentBatch_DestinationWarehouseId",
                table: "ShipmentBatches",
                newName: "IX_ShipmentBatches_DestinationWarehouseId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ShipmentBatches",
                table: "ShipmentBatches",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_ShipmentBatches_ShipmentBatchId",
                table: "Orders",
                column: "ShipmentBatchId",
                principalTable: "ShipmentBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentBatches_Employees_ShipperId",
                table: "ShipmentBatches",
                column: "ShipperId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentBatches_Warehouses_DestinationWarehouseId",
                table: "ShipmentBatches",
                column: "DestinationWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentBatches_Warehouses_OriginWarehouseId",
                table: "ShipmentBatches",
                column: "OriginWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_ShipmentBatches_ShipmentBatchId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentBatches_Employees_ShipperId",
                table: "ShipmentBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentBatches_Warehouses_DestinationWarehouseId",
                table: "ShipmentBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentBatches_Warehouses_OriginWarehouseId",
                table: "ShipmentBatches");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ShipmentBatches",
                table: "ShipmentBatches");

            migrationBuilder.RenameTable(
                name: "ShipmentBatches",
                newName: "ShipmentBatch");

            migrationBuilder.RenameIndex(
                name: "IX_ShipmentBatches_ShipperId",
                table: "ShipmentBatch",
                newName: "IX_ShipmentBatch_ShipperId");

            migrationBuilder.RenameIndex(
                name: "IX_ShipmentBatches_OriginWarehouseId",
                table: "ShipmentBatch",
                newName: "IX_ShipmentBatch_OriginWarehouseId");

            migrationBuilder.RenameIndex(
                name: "IX_ShipmentBatches_DestinationWarehouseId",
                table: "ShipmentBatch",
                newName: "IX_ShipmentBatch_DestinationWarehouseId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ShipmentBatch",
                table: "ShipmentBatch",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_ShipmentBatch_ShipmentBatchId",
                table: "Orders",
                column: "ShipmentBatchId",
                principalTable: "ShipmentBatch",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentBatch_Employees_ShipperId",
                table: "ShipmentBatch",
                column: "ShipperId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentBatch_Warehouses_DestinationWarehouseId",
                table: "ShipmentBatch",
                column: "DestinationWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentBatch_Warehouses_OriginWarehouseId",
                table: "ShipmentBatch",
                column: "OriginWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
