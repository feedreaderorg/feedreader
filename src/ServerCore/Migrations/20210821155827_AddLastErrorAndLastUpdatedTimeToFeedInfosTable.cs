using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FeedReader.ServerCore.Migrations
{
    public partial class AddLastErrorAndLastUpdatedTimeToFeedInfosTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastParseError",
                table: "FeedInfos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedTime",
                table: "FeedInfos",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "TotalSubscribers",
                table: "FeedInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastParseError",
                table: "FeedInfos");

            migrationBuilder.DropColumn(
                name: "LastUpdatedTime",
                table: "FeedInfos");

            migrationBuilder.DropColumn(
                name: "TotalSubscribers",
                table: "FeedInfos");
        }
    }
}
