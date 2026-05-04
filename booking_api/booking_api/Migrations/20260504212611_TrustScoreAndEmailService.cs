using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace booking_api.Migrations
{
    /// <inheritdoc />
    public partial class TrustScoreAndEmailService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastTrustAdjustment",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "TrustScore",
                table: "AspNetUsers",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.CreateTable(
                name: "TrustScoreHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousScore = table.Column<float>(type: "real", nullable: false),
                    NewScore = table.Column<float>(type: "real", nullable: false),
                    Adjustment = table.Column<float>(type: "real", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggeredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustScoreHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrustScoreHistory_AspNetUsers_TriggeredByUserId",
                        column: x => x.TriggeredByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TrustScoreHistory_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrustScoreHistory_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrustScoreHistory_BookingId",
                table: "TrustScoreHistory",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_TrustScoreHistory_TriggeredByUserId",
                table: "TrustScoreHistory",
                column: "TriggeredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TrustScoreHistory_UserId",
                table: "TrustScoreHistory",
                column: "UserId");

            migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"TrustScore\" = 50, \"LastTrustAdjustment\" = NOW() WHERE \"TrustScore\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrustScoreHistory");

            migrationBuilder.DropColumn(
                name: "LastTrustAdjustment",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TrustScore",
                table: "AspNetUsers");
        }
    }
}
