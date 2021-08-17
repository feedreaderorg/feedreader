using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FeedReader.WebServer.Migrations
{
    public partial class AddFeedInfoTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionName = table.Column<string>(type: "text", nullable: true),
                    Uri = table.Column<string>(type: "text", nullable: true),
                    IconUri = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    WebsiteLink = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedInfos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedInfos_SubscriptionName",
                table: "FeedInfos",
                column: "SubscriptionName",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedInfos");
        }
    }
}
