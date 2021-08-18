﻿// <auto-generated />
using System;
using FeedReader.WebServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace FeedReader.WebServer.Migrations
{
    [DbContext(typeof(DbContext))]
    [Migration("20210818190349_AddIdFromUriAndRegistrationTimeToFeedInfoTable")]
    partial class AddIdFromUriAndRegistrationTimeToFeedInfoTable
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Relational:MaxIdentifierLength", 63)
                .HasAnnotation("ProductVersion", "5.0.9")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            modelBuilder.Entity("FeedReader.WebServer.Models.FeedInfo", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<string>("IconUri")
                        .HasColumnType("text");

                    b.Property<Guid>("IdFromUri")
                        .HasColumnType("uuid");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.Property<DateTime>("RegistrationTime")
                        .HasColumnType("timestamp without time zone");

                    b.Property<string>("SubscriptionName")
                        .HasColumnType("text");

                    b.Property<string>("Uri")
                        .HasColumnType("text");

                    b.Property<string>("WebsiteLink")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("IdFromUri")
                        .IsUnique();

                    b.HasIndex("SubscriptionName")
                        .IsUnique();

                    b.ToTable("FeedInfos");
                });

            modelBuilder.Entity("FeedReader.WebServer.Models.File", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<byte[]>("Content")
                        .HasColumnType("bytea");

                    b.Property<DateTime>("CreationTime")
                        .HasColumnType("timestamp without time zone");

                    b.Property<string>("MimeType")
                        .HasColumnType("text");

                    b.Property<long>("Size")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.ToTable("Files");
                });

            modelBuilder.Entity("FeedReader.WebServer.Models.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("RegistrationTime")
                        .HasColumnType("timestamp without time zone");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("FeedReader.WebServer.Models.UserOAuthIds", b =>
                {
                    b.Property<string>("OAuthIssuer")
                        .HasColumnType("text");

                    b.Property<string>("OAuthId")
                        .HasColumnType("text");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("OAuthIssuer", "OAuthId");

                    b.ToTable("UserOAuthIds");
                });
#pragma warning restore 612, 618
        }
    }
}
