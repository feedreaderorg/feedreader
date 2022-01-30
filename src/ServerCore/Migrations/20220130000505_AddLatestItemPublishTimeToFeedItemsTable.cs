using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedReader.ServerCore.Migrations
{
    public partial class AddLatestItemPublishTimeToFeedItemsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LatestItemPublishTime",
                table: "FeedInfos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatestItemPublishTime",
                table: "FeedInfos");
        }
    }
}
