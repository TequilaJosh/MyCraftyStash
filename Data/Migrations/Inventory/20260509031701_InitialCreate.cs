using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCraftyStash.Data.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "address_book",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    first_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    address_line1 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    address_line2 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    city = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    zip_code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_address_book", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "calendar_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    event_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    event_time = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    reminder_minutes_before = table.Column<int>(type: "INTEGER", nullable: false),
                    color = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    is_all_day = table.Column<bool>(type: "INTEGER", nullable: false),
                    reminder_dismissed = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hidden_inspiration_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    image_key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hidden_inspiration_images", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inspiration_boards",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    parent_board_id = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    display_order = table.Column<int>(type: "INTEGER", nullable: false),
                    default_types = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    default_themes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    default_colors = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    default_sentiment = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    default_te_colors = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DefaultSubtypes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DefaultItemIds = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inspiration_boards", x => x.id);
                    table.ForeignKey(
                        name: "FK_inspiration_boards_inspiration_boards_parent_board_id",
                        column: x => x.parent_board_id,
                        principalTable: "inspiration_boards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "items",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    location = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    theme = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    sentiments = table.Column<string>(type: "TEXT", nullable: true),
                    image_url = table.Column<string>(type: "TEXT", nullable: true),
                    price = table.Column<decimal>(type: "TEXT", nullable: true),
                    date_purchased = table.Column<DateTime>(type: "TEXT", nullable: true),
                    item_number = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    is_discontinued = table.Column<bool>(type: "INTEGER", maxLength: 30, nullable: false),
                    subtype = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    stencil_layers = table.Column<int>(type: "INTEGER", nullable: true),
                    pack_size = table.Column<int>(type: "INTEGER", nullable: true),
                    current_stock = table.Column<int>(type: "INTEGER", nullable: true),
                    purchased_from = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    site_url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    image_url = table.Column<string>(type: "TEXT", nullable: true),
                    technique = table.Column<string>(type: "TEXT", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wishlists",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    color = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wishlists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inspiration_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    image_url = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    board_id = table.Column<int>(type: "INTEGER", nullable: true),
                    color = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    types = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    theme = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    sentiment = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    te_color = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inspiration_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_inspiration_images_inspiration_boards_board_id",
                        column: x => x.board_id,
                        principalTable: "inspiration_boards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "item_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    item_id = table.Column<int>(type: "INTEGER", nullable: false),
                    image_url = table.Column<string>(type: "TEXT", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_images_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_purchases",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    item_id = table.Column<int>(type: "INTEGER", nullable: false),
                    quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    price_per_item = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    date_purchased = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_purchases", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_purchases_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_relationships",
                columns: table => new
                {
                    item_id = table.Column<int>(type: "INTEGER", nullable: false),
                    related_item_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_relationships", x => new { x.item_id, x.related_item_id });
                    table.ForeignKey(
                        name: "FK_item_relationships_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_item_relationships_items_related_item_id",
                        column: x => x.related_item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sentiment_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    item_id = table.Column<int>(type: "INTEGER", nullable: false),
                    image_data = table.Column<string>(type: "TEXT", nullable: false),
                    extracted_text = table.Column<string>(type: "TEXT", nullable: false),
                    search_text = table.Column<string>(type: "TEXT", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sentiment_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_sentiment_images_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stacklet_dies",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    item_id = table.Column<int>(type: "INTEGER", nullable: false),
                    die_number = table.Column<int>(type: "INTEGER", nullable: false),
                    width = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    height = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stacklet_dies", x => x.id);
                    table.ForeignKey(
                        name: "FK_stacklet_dies_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_card_builds",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    card_base_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    state_snapshot = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_card_builds", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_card_builds_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_creations",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    materials_used = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_creations", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_creations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    image_url = table.Column<string>(type: "TEXT", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_images_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    item_id = table.Column<int>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    amount_used_per_creation = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_items_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_items_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wishlist_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    item_number = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    theme = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    price = table.Column<decimal>(type: "TEXT", nullable: true),
                    image_url = table.Column<string>(type: "TEXT", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    priority = table.Column<int>(type: "INTEGER", nullable: false),
                    purchased_from = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    wishlist_id = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wishlist_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_wishlist_items_wishlists_wishlist_id",
                        column: x => x.wishlist_id,
                        principalTable: "wishlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "inspiration_image_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    inspiration_image_id = table.Column<int>(type: "INTEGER", nullable: false),
                    item_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inspiration_image_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_inspiration_image_items_inspiration_images_inspiration_image_id",
                        column: x => x.inspiration_image_id,
                        principalTable: "inspiration_images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inspiration_image_items_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_card_build_steps",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    build_id = table.Column<int>(type: "INTEGER", nullable: false),
                    step_order = table.Column<int>(type: "INTEGER", nullable: false),
                    section = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    step_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    mat_layer = table.Column<int>(type: "INTEGER", nullable: true),
                    item_id = table.Column<int>(type: "INTEGER", nullable: true),
                    stacklet_die_id = table.Column<int>(type: "INTEGER", nullable: true),
                    cutting_method = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    label = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_card_build_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_card_build_steps_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_project_card_build_steps_project_card_builds_build_id",
                        column: x => x.build_id,
                        principalTable: "project_card_builds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_card_build_steps_stacklet_dies_stacklet_die_id",
                        column: x => x.stacklet_die_id,
                        principalTable: "stacklet_dies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inspiration_boards_parent_board_id",
                table: "inspiration_boards",
                column: "parent_board_id");

            migrationBuilder.CreateIndex(
                name: "IX_inspiration_image_items_inspiration_image_id",
                table: "inspiration_image_items",
                column: "inspiration_image_id");

            migrationBuilder.CreateIndex(
                name: "IX_inspiration_image_items_item_id",
                table: "inspiration_image_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_inspiration_images_board_id",
                table: "inspiration_images",
                column: "board_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_images_item_id",
                table: "item_images",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_purchases_item_id",
                table: "item_purchases",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_relationships_related_item_id",
                table: "item_relationships",
                column: "related_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_card_build_steps_build_id",
                table: "project_card_build_steps",
                column: "build_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_card_build_steps_item_id",
                table: "project_card_build_steps",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_card_build_steps_stacklet_die_id",
                table: "project_card_build_steps",
                column: "stacklet_die_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_card_builds_project_id",
                table: "project_card_builds",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_creations_project_id",
                table: "project_creations",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_images_project_id",
                table: "project_images",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_items_item_id",
                table: "project_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_items_project_id",
                table: "project_items",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_sentiment_images_item_id",
                table: "sentiment_images",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_stacklet_dies_item_id",
                table: "stacklet_dies",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_wishlist_items_wishlist_id",
                table: "wishlist_items",
                column: "wishlist_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "address_book");

            migrationBuilder.DropTable(
                name: "calendar_events");

            migrationBuilder.DropTable(
                name: "hidden_inspiration_images");

            migrationBuilder.DropTable(
                name: "inspiration_image_items");

            migrationBuilder.DropTable(
                name: "item_images");

            migrationBuilder.DropTable(
                name: "item_purchases");

            migrationBuilder.DropTable(
                name: "item_relationships");

            migrationBuilder.DropTable(
                name: "project_card_build_steps");

            migrationBuilder.DropTable(
                name: "project_creations");

            migrationBuilder.DropTable(
                name: "project_images");

            migrationBuilder.DropTable(
                name: "project_items");

            migrationBuilder.DropTable(
                name: "sentiment_images");

            migrationBuilder.DropTable(
                name: "wishlist_items");

            migrationBuilder.DropTable(
                name: "inspiration_images");

            migrationBuilder.DropTable(
                name: "project_card_builds");

            migrationBuilder.DropTable(
                name: "stacklet_dies");

            migrationBuilder.DropTable(
                name: "wishlists");

            migrationBuilder.DropTable(
                name: "inspiration_boards");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "items");
        }
    }
}
