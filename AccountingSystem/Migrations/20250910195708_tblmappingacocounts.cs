using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class tblmappingacocounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentVouchers_AspNetUsers_CreatedById",
                table: "PaymentVouchers");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedById",
                table: "PaymentVouchers",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateTable(
                name: "CusomerMappingAccounts",
                columns: table => new
                {
                    CustomerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CusomerMappingAccounts", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "DriverMappingAccounts",
                columns: table => new
                {
                    DriverId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverMappingAccounts", x => x.DriverId);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentVouchers_AspNetUsers_CreatedById",
                table: "PaymentVouchers",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentVouchers_AspNetUsers_CreatedById",
                table: "PaymentVouchers");

            migrationBuilder.DropTable(
                name: "CusomerMappingAccounts");

            migrationBuilder.DropTable(
                name: "DriverMappingAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedById",
                table: "PaymentVouchers",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentVouchers_AspNetUsers_CreatedById",
                table: "PaymentVouchers",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
