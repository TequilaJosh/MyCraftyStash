-- =============================================================================
--  My Crafty Stash — Wipe Demo Data
--  Removes everything seeded by SeedDemoData.sql (id >= 9000 in every table).
--  Leaves real user rows (smaller ids) untouched.
--
--  Order matters: delete child rows before parents to satisfy FK cascades on
--  SQLite when the connection has foreign_keys = ON.
-- =============================================================================

BEGIN TRANSACTION;

-- Card build steps before builds
DELETE FROM project_card_build_steps WHERE id >= 9000;
DELETE FROM project_card_builds      WHERE id >= 9000;

-- Project children before projects
DELETE FROM project_creations WHERE id >= 9000;
DELETE FROM project_items     WHERE id >= 9000;
DELETE FROM project_images    WHERE id >= 9000;
DELETE FROM projects          WHERE id >= 9000;

-- Wishlist items before lists
DELETE FROM wishlist_items WHERE id >= 9000;
DELETE FROM wishlists      WHERE id >= 9000;

-- Inspiration children before boards
DELETE FROM inspiration_images WHERE id >= 9000;
DELETE FROM inspiration_boards WHERE id >= 9000;

-- Item children before items
DELETE FROM sentiment_images   WHERE id >= 9000;
DELETE FROM item_purchases     WHERE id >= 9000;
DELETE FROM item_relationships WHERE item_id >= 9000 OR related_item_id >= 9000;
DELETE FROM items              WHERE id >= 9000;

-- Standalone tables
DELETE FROM calendar_events WHERE id >= 9000;
DELETE FROM address_book    WHERE id >= 9000;

COMMIT;

-- VACUUM is optional but reclaims space after a big delete:
-- VACUUM;
