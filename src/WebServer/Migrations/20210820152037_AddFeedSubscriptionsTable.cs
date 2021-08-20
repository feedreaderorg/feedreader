using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FeedReader.WebServer.Migrations
{
    public partial class AddFeedSubscriptionsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedSubscriptions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedSubscriptions", x => new { x.UserId, x.FeedId });
                    table.ForeignKey(
                        name: "FK_FeedSubscriptions_FeedInfos_FeedId",
                        column: x => x.FeedId,
                        principalTable: "FeedInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeedSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedSubscriptions_FeedId",
                table: "FeedSubscriptions",
                column: "FeedId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedSubscriptions_UserId",
                table: "FeedSubscriptions",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedSubscriptions");
        }
    }
}
