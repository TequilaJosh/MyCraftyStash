-- =============================================================================
--  My Crafty Stash — Demo Data Seed
--  See README.md in this folder for what this produces and how to apply it.
--
--  Conventions:
--    * Every row uses an explicit id >= 9000 so WipeDemoData.sql can clean up
--      with WHERE id >= 9000 without touching real user data.
--    * All dates use SQLite's date()/datetime() relative-to-now functions so
--      the Home dashboard's "this month" deltas stay meaningful whenever the
--      seed is applied.
--    * Wrapped in a single transaction — if any INSERT fails, nothing commits.
-- =============================================================================

BEGIN TRANSACTION;

-- -----------------------------------------------------------------------------
--  ITEMS (40 rows, ids 9001..9040)
--  Covers: stamps, dies, cardstock (colored), stencils, embellishments, paper.
--  Stock levels chosen so 5 land in the LOW/OUT bucket on the Home dashboard.
-- -----------------------------------------------------------------------------

-- Sentiment stamp sets (sentiments column is the searchable text bank)
INSERT INTO items (id, name, type, subtype, theme, sentiments, image_url, price, date_purchased, item_number, is_discontinued, current_stock, pack_size, purchased_from, notes, created_at) VALUES
(9001, 'Big Thanks Sentiment Set', 'Stamp', 'Sentiment Set', 'Thank You', 'thank you|thanks a million|so very thankful|with gratitude|thank you kindly|many thanks', 'https://picsum.photos/seed/mcs9001/400/400', '14.99', date('now', '-280 days'), 'TE-STM-1001', 0, 4, NULL, 'Taylored Expressions', 'Workhorse thank-you set — used on most cards', datetime('now', '-280 days')),
(9002, 'Birthday Wishes Sentiment Set', 'Stamp', 'Sentiment Set', 'Birthday', 'happy birthday|wishing you a wonderful day|cheers to you|another year better|make a wish|birthday hugs', 'https://picsum.photos/seed/mcs9002/400/400', '12.99', date('now', '-200 days'), 'TE-STM-1002', 0, 8, NULL, 'Taylored Expressions', NULL, datetime('now', '-200 days')),
(9003, 'Sympathy Sentiments', 'Stamp', 'Sentiment Set', 'Sympathy', 'with sympathy|thinking of you|so sorry for your loss|holding you in my heart|sending love', 'https://picsum.photos/seed/mcs9003/400/400', '13.99', date('now', '-150 days'), 'TE-STM-1003', 0, 3, NULL, 'Simon Says Stamp', NULL, datetime('now', '-150 days')),
(9004, 'Christmas Greetings', 'Stamp', 'Sentiment Set', 'Christmas', 'merry christmas|joy to the world|warmest wishes|peace on earth|happy holidays', 'https://picsum.photos/seed/mcs9004/400/400', '15.99', date('now', '-95 days'), 'TE-STM-1004', 0, 5, NULL, 'Taylored Expressions', NULL, datetime('now', '-95 days')),
(9005, 'Encouragement Words', 'Stamp', 'Sentiment Set', 'Encouragement', 'you got this|sending strength|believe in you|brighter days ahead|thinking of you', 'https://picsum.photos/seed/mcs9005/400/400', '12.99', date('now', '-60 days'), 'TE-STM-1005', 0, 6, NULL, 'Concord & 9th', NULL, datetime('now', '-60 days'));

-- Image/focal stamp sets
INSERT INTO items (id, name, type, subtype, theme, sentiments, image_url, price, date_purchased, item_number, is_discontinued, current_stock, pack_size, purchased_from, notes, created_at) VALUES
(9006, 'Cottage Garden Florals', 'Stamp', 'Image Set', 'Floral', '', 'https://picsum.photos/seed/mcs9006/400/400', '24.99', date('now', '-220 days'), 'TE-STM-2001', 0, 2, NULL, 'Taylored Expressions', 'Coordinating dies in #9011', datetime('now', '-220 days')),
(9007, 'Forest Friends', 'Stamp', 'Image Set', 'Animals', '', 'https://picsum.photos/seed/mcs9007/400/400', '22.99', date('now', '-180 days'), 'TE-STM-2002', 0, 3, NULL, 'Lawn Fawn', NULL, datetime('now', '-180 days')),
(9008, 'Vintage Roses', 'Stamp', 'Image Set', 'Floral', '', 'https://picsum.photos/seed/mcs9008/400/400', '19.99', date('now', '-300 days'), 'TE-STM-2003', 1, 0, NULL, 'Hero Arts', 'Discontinued, retired Jan 2026', datetime('now', '-300 days')),
(9009, 'Birthday Balloons', 'Stamp', 'Image Set', 'Birthday', '', 'https://picsum.photos/seed/mcs9009/400/400', '17.99', date('now', '-120 days'), 'TE-STM-2004', 0, 4, NULL, 'Taylored Expressions', NULL, datetime('now', '-120 days')),
(9010, 'Holly & Berries', 'Stamp', 'Image Set', 'Christmas', '', 'https://picsum.photos/seed/mcs9010/400/400', '18.99', date('now', '-80 days'), 'TE-STM-2005', 0, 5, NULL, 'Taylored Expressions', NULL, datetime('now', '-80 days'));

-- Dies (coordinating + standalone)
INSERT INTO items (id, name, type, subtype, theme, sentiments, image_url, price, date_purchased, item_number, is_discontinued, current_stock, pack_size, purchased_from, notes, created_at) VALUES
(9011, 'Cottage Garden Florals — Coordinating Dies', 'Die', 'Coordinating Dies', 'Floral', '', 'https://picsum.photos/seed/mcs9011/400/400', '29.99', date('now', '-220 days'), 'TE-DIE-3001', 0, 2, NULL, 'Taylored Expressions', 'Pairs with #9006', datetime('now', '-220 days')),
(9012, 'Forest Friends — Coordinating Dies', 'Die', 'Coordinating Dies', 'Animals', '', 'https://picsum.photos/seed/mcs9012/400/400', '27.99', date('now', '-180 days'), 'TE-DIE-3002', 0, 3, NULL, 'Lawn Fawn', NULL, datetime('now', '-180 days')),
(9013, 'Stitched Rectangles', 'Die', 'Frame', 'Basics', '', 'https://picsum.photos/seed/mcs9013/400/400', '34.99', date('now', '-400 days'), 'TE-DIE-3003', 0, 1, NULL, 'Taylored Expressions', 'Workhorse — keep in dies drawer top shelf', datetime('now', '-400 days')),
(9014, 'Scalloped Circles', 'Die', 'Frame', 'Basics', '', 'https://picsum.photos/seed/mcs9014/400/400', '24.99', date('now', '-260 days'), 'TE-DIE-3004', 0, 2, NULL, 'Spellbinders', NULL, datetime('now', '-260 days')),
(9015, 'Sentiment Banners', 'Die', 'Sentiment Die', 'Basics', '', 'https://picsum.photos/seed/mcs9015/400/400', '19.99', date('now', '-100 days'), 'TE-DIE-3005', 0, 4, NULL, 'Concord & 9th', NULL, datetime('now', '-100 days'));

-- Cardstock (color-named — this drives the Color Match demo)
-- location names are arbitrary "where in the craft room" labels
INSERT INTO items (id, name, type, subtype, theme, sentiments, image_url, price, date_purchased, item_number, is_discontinued, current_stock, pack_size, purchased_from, notes, location, created_at) VALUES
(9016, 'Crimson Cardstock', 'Cardstock', 'Solid', 'Red', '', 'https://picsum.photos/seed/mcs9016/400/400', '8.99', date('now', '-90 days'), 'TE-CS-4001', 0, 12, 25, 'Taylored Expressions', NULL, 'Drawer B1', datetime('now', '-90 days')),
(9017, 'Sage Cardstock', 'Cardstock', 'Solid', 'Green', '', 'https://picsum.photos/seed/mcs9017/400/400', '8.99', date('now', '-90 days'), 'TE-CS-4002', 0, 18, 25, 'Taylored Expressions', NULL, 'Drawer B1', datetime('now', '-90 days')),
(9018, 'Lemon Cardstock', 'Cardstock', 'Solid', 'Yellow', '', 'https://picsum.photos/seed/mcs9018/400/400', '8.99', date('now', '-90 days'), 'TE-CS-4003', 0, 1, 25, 'Taylored Expressions', NULL, 'Drawer B1', datetime('now', '-90 days')),
(9019, 'Sky Cardstock', 'Cardstock', 'Solid', 'Blue', '', 'https://picsum.photos/seed/mcs9019/400/400', '8.99', date('now', '-90 days'), 'TE-CS-4004', 0, 9, 25, 'Taylored Expressions', NULL, 'Drawer B1', datetime('now', '-90 days')),
(9020, 'Charcoal Cardstock', 'Cardstock', 'Solid', 'Gray', '', 'https://picsum.photos/seed/mcs9020/400/400', '8.99', date('now', '-90 days'), 'TE-CS-4005', 0, 7, 25, 'Taylored Expressions', NULL, 'Drawer B1', datetime('now', '-90 days')),
(9021, 'Cream Cardstock', 'Cardstock', 'Solid', 'Neutral', '', 'https://picsum.photos/seed/mcs9021/400/400', '8.99', date('now', '-360 days'), 'TE-CS-4006', 0, 0, 25, 'Taylored Expressions', 'Out of stock — reorder', 'Drawer B1', datetime('now', '-360 days')),
(9022, 'White Cardstock', 'Cardstock', 'Solid', 'White', '', 'https://picsum.photos/seed/mcs9022/400/400', '9.99', date('now', '-180 days'), 'TE-CS-4007', 0, 35, 50, 'Neenah', 'Workhorse — buy in bulk', 'Drawer B1', datetime('now', '-180 days')),
(9023, 'Kraft Cardstock', 'Cardstock', 'Textured', 'Neutral', '', 'https://picsum.photos/seed/mcs9023/400/400', '7.99', date('now', '-220 days'), 'TE-CS-4008', 0, 14, 25, 'Hobby Lobby', NULL, 'Drawer B2', datetime('now', '-220 days')),
(9024, 'Coral Cardstock', 'Cardstock', 'Solid', 'Pink', '', 'https://picsum.photos/seed/mcs9024/400/400', '8.99', date('now', '-30 days'), 'TE-CS-4009', 0, 6, 25, 'Taylored Expressions', NULL, 'Drawer B1', datetime('now', '-25 days')),
(9025, 'Plum Cardstock', 'Cardstock', 'Solid', 'Purple', '', 'https://picsum.photos/seed/mcs9025/400/400', '8.99', date('now', '-30 days'), 'TE-CS-4010', 0, 5, 25, 'Taylored Expressions', NULL, 'Drawer B1', datetime('now', '-25 days'));

-- Stencils
INSERT INTO items (id, name, type, subtype, theme, sentiments, image_url, price, date_purchased, item_number, is_discontinued, stencil_layers, current_stock, pack_size, purchased_from, notes, created_at) VALUES
(9026, 'Botanical Layers Stencil', 'Stencil', 'Layering', 'Floral', '', 'https://picsum.photos/seed/mcs9026/400/400', '15.99', date('now', '-150 days'), 'TE-STN-5001', 0, 3, 2, NULL, 'Taylored Expressions', NULL, datetime('now', '-150 days')),
(9027, 'Geometric Background Stencil', 'Stencil', 'Background', 'Modern', '', 'https://picsum.photos/seed/mcs9027/400/400', '11.99', date('now', '-200 days'), 'TE-STN-5002', 0, 1, 1, NULL, 'Picket Fence Studios', NULL, datetime('now', '-200 days')),
(9028, 'Snowflake Stencil', 'Stencil', 'Background', 'Christmas', '', 'https://picsum.photos/seed/mcs9028/400/400', '12.99', date('now', '-340 days'), 'TE-STN-5003', 0, 1, 0, NULL, 'Taylored Expressions', 'Out of stock', datetime('now', '-340 days'));

-- Embellishments
INSERT INTO items (id, name, type, subtype, theme, sentiments, image_url, price, date_purchased, item_number, is_discontinued, current_stock, pack_size, purchased_from, notes, created_at) VALUES
(9029, 'Iridescent Sequins Mix', 'Embellishment', 'Sequins', 'Basics', '', 'https://picsum.photos/seed/mcs9029/400/400', '6.99', date('now', '-110 days'), 'TE-EMB-6001', 0, 4, NULL, 'Pretty Pink Posh', 'Mixed sizes 4mm-8mm', datetime('now', '-110 days')),
(9030, 'Gold Foil Stars', 'Embellishment', 'Sequins', 'Birthday', '', 'https://picsum.photos/seed/mcs9030/400/400', '5.99', date('now', '-50 days'), 'TE-EMB-6002', 0, 2, NULL, 'Pretty Pink Posh', NULL, datetime('now', '-50 days')),
(9031, 'Linen Ribbon — Cream', 'Embellishment', 'Ribbon', 'Basics', '', 'https://picsum.photos/seed/mcs9031/400/400', '9.99', date('now', '-280 days'), 'TE-EMB-6003', 0, 1, NULL, 'May Arts', '5/8 inch, 10 yard spool', datetime('now', '-280 days')),
(9032, 'Sparkle Glitter Stars', 'Embellishment', 'Sequins', 'Christmas', '', 'https://picsum.photos/seed/mcs9032/400/400', '6.99', date('now', '-75 days'), 'TE-EMB-6004', 0, 3, NULL, 'Studio Katia', NULL, datetime('now', '-75 days')),
(9033, 'Pearl Drops — White', 'Embellishment', 'Pearls', 'Basics', '', 'https://picsum.photos/seed/mcs9033/400/400', '4.99', date('now', '-300 days'), 'TE-EMB-6005', 0, 0, NULL, 'Pretty Pink Posh', 'Out of stock', datetime('now', '-300 days'));

-- Paper / patterned
INSERT INTO items (id, name, type, subtype, theme, sentiments, image_url, price, date_purchased, item_number, is_discontinued, current_stock, pack_size, purchased_from, notes, created_at) VALUES
(9034, 'Floral Spring 6x6 Paper Pack', 'Paper', 'Patterned', 'Floral', '', 'https://picsum.photos/seed/mcs9034/400/400', '12.99', date('now', '-45 days'), 'TE-PPR-7001', 0, 2, 24, 'Taylored Expressions', NULL, datetime('now', '-25 days')),
(9035, 'Christmas Plaid 6x6 Paper Pack', 'Paper', 'Patterned', 'Christmas', '', 'https://picsum.photos/seed/mcs9035/400/400', '12.99', date('now', '-110 days'), 'TE-PPR-7002', 0, 1, 24, 'Echo Park', NULL, datetime('now', '-110 days')),
(9036, 'Vintage Botanical 12x12 Paper Pack', 'Paper', 'Patterned', 'Floral', '', 'https://picsum.photos/seed/mcs9036/400/400', '15.99', date('now', '-200 days'), 'TE-PPR-7003', 1, 0, 12, 'Hero Arts', 'Discontinued', datetime('now', '-200 days'));

-- Inks
INSERT INTO items (id, name, type, subtype, theme, sentiments, image_url, price, date_purchased, item_number, is_discontinued, current_stock, pack_size, purchased_from, notes, created_at) VALUES
(9037, 'Versafine Black Ink Pad', 'Ink', 'Pigment', 'Basics', '', 'https://picsum.photos/seed/mcs9037/400/400', '7.99', date('now', '-500 days'), 'TE-INK-8001', 0, 1, NULL, 'Tsukineko', 'Workhorse — daily use', datetime('now', '-500 days')),
(9038, 'Distress Oxide — Salty Ocean', 'Ink', 'Oxide', 'Basics', '', 'https://picsum.photos/seed/mcs9038/400/400', '5.99', date('now', '-280 days'), 'TE-INK-8002', 0, 1, NULL, 'Ranger', NULL, datetime('now', '-280 days')),
(9039, 'Distress Oxide — Picked Raspberry', 'Ink', 'Oxide', 'Basics', '', 'https://picsum.photos/seed/mcs9039/400/400', '5.99', date('now', '-280 days'), 'TE-INK-8003', 0, 1, NULL, 'Ranger', NULL, datetime('now', '-280 days'));

-- Recent adds — pushes Home dashboard "this month" delta to a positive number
INSERT INTO items (id, name, type, subtype, theme, sentiments, image_url, price, date_purchased, item_number, is_discontinued, current_stock, pack_size, purchased_from, notes, created_at) VALUES
(9040, 'Spring 2026 Release — Tulip Stamps', 'Stamp', 'Image Set', 'Floral', '', 'https://picsum.photos/seed/mcs9040/400/400', '19.99', date('now', '-12 days'), 'TE-STM-2026A', 0, 1, NULL, 'Taylored Expressions', 'New release — Spring 2026', datetime('now', '-12 days'));

-- -----------------------------------------------------------------------------
--  ITEM RELATIONSHIPS — coordinating stamps/dies linked both directions
-- -----------------------------------------------------------------------------
INSERT INTO item_relationships (item_id, related_item_id) VALUES
(9006, 9011), (9011, 9006),  -- Cottage Garden Florals ↔ its dies
(9007, 9012), (9012, 9007),  -- Forest Friends ↔ its dies
(9009, 9015), (9015, 9009),  -- Birthday Balloons ↔ Sentiment Banners
(9010, 9013), (9013, 9010);  -- Holly & Berries ↔ Stitched Rectangles

-- -----------------------------------------------------------------------------
--  ITEM PURCHASES — purchase-history rows so the Item Detail card looks lived-in
--  Also feeds the Purchase Report totals. Quantity * price_per_item ≈ totals.
-- -----------------------------------------------------------------------------
INSERT INTO item_purchases (id, item_id, quantity, price_per_item, date_purchased, created_at) VALUES
-- White cardstock — bought in bulk multiple times
(9001, 9022, 1, 9.99, date('now', '-180 days'), datetime('now', '-180 days')),
(9002, 9022, 1, 9.99, date('now', '-110 days'), datetime('now', '-110 days')),
(9003, 9022, 2, 9.49, date('now', '-45 days'), datetime('now', '-45 days')),
(9004, 9022, 1, 9.99, date('now', '-10 days'), datetime('now', '-10 days')),
-- Crimson cardstock
(9005, 9016, 1, 8.99, date('now', '-90 days'), datetime('now', '-90 days')),
(9006, 9016, 1, 8.99, date('now', '-15 days'), datetime('now', '-15 days')),
-- Sage cardstock
(9007, 9017, 2, 8.49, date('now', '-90 days'), datetime('now', '-90 days')),
-- Big Thanks sentiment set
(9008, 9001, 1, 14.99, date('now', '-280 days'), datetime('now', '-280 days')),
-- Birthday Wishes
(9009, 9002, 1, 12.99, date('now', '-200 days'), datetime('now', '-200 days')),
-- Cottage Garden Florals + dies — bought as a bundle
(9010, 9006, 1, 24.99, date('now', '-220 days'), datetime('now', '-220 days')),
(9011, 9011, 1, 29.99, date('now', '-220 days'), datetime('now', '-220 days')),
-- Forest Friends bundle
(9012, 9007, 1, 22.99, date('now', '-180 days'), datetime('now', '-180 days')),
(9013, 9012, 1, 27.99, date('now', '-180 days'), datetime('now', '-180 days')),
-- Recent embellishment haul
(9014, 9029, 1, 6.99, date('now', '-110 days'), datetime('now', '-110 days')),
(9015, 9030, 2, 5.99, date('now', '-50 days'), datetime('now', '-50 days')),
(9016, 9032, 1, 6.99, date('now', '-75 days'), datetime('now', '-75 days')),
-- New tulip release (recent)
(9017, 9040, 1, 19.99, date('now', '-12 days'), datetime('now', '-12 days')),
-- Christmas inventory haul
(9018, 9004, 1, 15.99, date('now', '-95 days'), datetime('now', '-95 days')),
(9019, 9010, 1, 18.99, date('now', '-80 days'), datetime('now', '-80 days')),
(9020, 9035, 1, 12.99, date('now', '-110 days'), datetime('now', '-110 days'));

-- -----------------------------------------------------------------------------
--  SENTIMENT IMAGES — for the "Sentiments from this Set" card on Item Detail.
--  image_data is base64 of a tiny placeholder PNG (1px sage-green square).
--  search_text mirrors extracted_text for the search index.
-- -----------------------------------------------------------------------------
INSERT INTO sentiment_images (id, item_id, image_data, extracted_text, search_text, sort_order, created_at) VALUES
(9001, 9001, 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkqAcAAIUAgUW0RjgAAAAASUVORK5CYII=', 'thank you', 'thank you', 0, datetime('now', '-280 days')),
(9002, 9001, 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkqAcAAIUAgUW0RjgAAAAASUVORK5CYII=', 'so very thankful', 'so very thankful', 1, datetime('now', '-280 days')),
(9003, 9001, 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkqAcAAIUAgUW0RjgAAAAASUVORK5CYII=', 'with gratitude', 'with gratitude', 2, datetime('now', '-280 days')),
(9004, 9002, 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkqAcAAIUAgUW0RjgAAAAASUVORK5CYII=', 'happy birthday', 'happy birthday', 0, datetime('now', '-200 days')),
(9005, 9002, 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkqAcAAIUAgUW0RjgAAAAASUVORK5CYII=', 'another year better', 'another year better', 1, datetime('now', '-200 days')),
(9006, 9004, 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkqAcAAIUAgUW0RjgAAAAASUVORK5CYII=', 'merry christmas', 'merry christmas', 0, datetime('now', '-95 days'));

-- -----------------------------------------------------------------------------
--  PROJECTS (8 rows, ids 9001..9008)
--  One is is_shared = 1 to show the "Shared" badge in the Projects grid.
-- -----------------------------------------------------------------------------
INSERT INTO projects (id, name, description, image_url, technique, notes, is_shared, shared_from_name, shared_at, created_at) VALUES
(9001, 'Sage Thank You Card', 'Clean & simple thanks card with sage cardstock and a die-cut sentiment banner.', 'https://picsum.photos/seed/mcsp9001/600/600', 'Stamping', 'Keep extras on hand for teacher gifts.', 0, NULL, NULL, datetime('now', '-92 days')),
(9002, 'Cottage Garden Birthday', 'Layered florals with the Cottage Garden set on a kraft base.', 'https://picsum.photos/seed/mcsp9002/600/600', 'Die Cutting + Stamping', NULL, 0, NULL, NULL, datetime('now', '-70 days')),
(9003, 'Forest Friends Birthday', 'Whimsical animal birthday card — kid favorite.', 'https://picsum.photos/seed/mcsp9003/600/600', 'Stamping + Coloring', 'Watercolor the animals first; let dry fully.', 0, NULL, NULL, datetime('now', '-58 days')),
(9004, 'Crimson Sympathy', 'Quiet sympathy card with soft sentiment and pearl drops.', 'https://picsum.photos/seed/mcsp9004/600/600', 'Stamping', NULL, 0, NULL, NULL, datetime('now', '-44 days')),
(9005, 'Holly Christmas Card', 'Holly stamped on white over kraft with a plaid frame.', 'https://picsum.photos/seed/mcsp9005/600/600', 'Stamping', NULL, 0, NULL, NULL, datetime('now', '-31 days')),
(9006, 'Tulip Spring Hello', 'Bright spring hello with the new tulip release.', 'https://picsum.photos/seed/mcsp9006/600/600', 'Stamping', 'Test layout — may revise.', 0, NULL, NULL, datetime('now', '-10 days')),
(9007, 'Encouragement — You Got This', 'Quick encouragement card for a friend going through a hard time.', 'https://picsum.photos/seed/mcsp9007/600/600', 'Stamping', NULL, 0, NULL, NULL, datetime('now', '-5 days')),
-- One imported / shared project — shows the "Shared" badge in the grid
(9008, 'Imported — Friend''s Sympathy Card', 'Sympathy design shared by Mary at the crafting circle. Imported via .mcsproject file.', 'https://picsum.photos/seed/mcsp9008/600/600', 'Stamping + Heat Embossing', NULL, 1, 'Mary K.', datetime('now', '-15 days'), datetime('now', '-15 days'));

-- -----------------------------------------------------------------------------
--  PROJECT IMAGES — gallery thumbnails (2 per project)
-- -----------------------------------------------------------------------------
INSERT INTO project_images (id, project_id, image_url, sort_order, created_at) VALUES
(9001, 9001, 'https://picsum.photos/seed/mcspg9001a/600/600', 0, datetime('now', '-92 days')),
(9002, 9001, 'https://picsum.photos/seed/mcspg9001b/600/600', 1, datetime('now', '-92 days')),
(9003, 9002, 'https://picsum.photos/seed/mcspg9002a/600/600', 0, datetime('now', '-70 days')),
(9004, 9002, 'https://picsum.photos/seed/mcspg9002b/600/600', 1, datetime('now', '-70 days')),
(9005, 9003, 'https://picsum.photos/seed/mcspg9003a/600/600', 0, datetime('now', '-58 days')),
(9006, 9003, 'https://picsum.photos/seed/mcspg9003b/600/600', 1, datetime('now', '-58 days')),
(9007, 9004, 'https://picsum.photos/seed/mcspg9004a/600/600', 0, datetime('now', '-44 days')),
(9008, 9005, 'https://picsum.photos/seed/mcspg9005a/600/600', 0, datetime('now', '-31 days')),
(9009, 9005, 'https://picsum.photos/seed/mcspg9005b/600/600', 1, datetime('now', '-31 days')),
(9010, 9006, 'https://picsum.photos/seed/mcspg9006a/600/600', 0, datetime('now', '-10 days')),
(9011, 9007, 'https://picsum.photos/seed/mcspg9007a/600/600', 0, datetime('now', '-5 days')),
(9012, 9008, 'https://picsum.photos/seed/mcspg9008a/600/600', 0, datetime('now', '-15 days')),
(9013, 9008, 'https://picsum.photos/seed/mcspg9008b/600/600', 1, datetime('now', '-15 days')),
(9014, 9002, 'https://picsum.photos/seed/mcspg9002c/600/600', 2, datetime('now', '-70 days')),
(9015, 9001, 'https://picsum.photos/seed/mcspg9001c/600/600', 2, datetime('now', '-92 days')),
(9016, 9003, 'https://picsum.photos/seed/mcspg9003c/600/600', 2, datetime('now', '-58 days'));

-- -----------------------------------------------------------------------------
--  PROJECT ITEMS — which items each project uses (Items Used sidebar)
--  Several items appear in multiple projects so the "Specific Item" filter
--  in the Projects view returns meaningful results.
-- -----------------------------------------------------------------------------
INSERT INTO project_items (id, project_id, item_id, sort_order, amount_used_per_creation) VALUES
-- Sage Thank You Card
(9001, 9001, 9001, 0, '1'),  -- Big Thanks stamp
(9002, 9001, 9017, 1, '1'),  -- Sage cardstock
(9003, 9001, 9022, 2, '1'),  -- White cardstock
(9004, 9001, 9015, 3, '1'),  -- Sentiment Banners die
(9005, 9001, 9037, 4, '1'),  -- Versafine black ink
-- Cottage Garden Birthday
(9006, 9002, 9006, 0, '1'),  -- Cottage Garden stamps
(9007, 9002, 9011, 1, '1'),  -- Cottage Garden dies
(9008, 9002, 9023, 2, '1'),  -- Kraft cardstock
(9009, 9002, 9022, 3, '1'),  -- White cardstock
(9010, 9002, 9002, 4, '1'),  -- Birthday Wishes sentiment
(9011, 9002, 9029, 5, '1'),  -- Iridescent sequins
-- Forest Friends Birthday
(9012, 9003, 9007, 0, '1'),
(9013, 9003, 9012, 1, '1'),
(9014, 9003, 9022, 2, '1'),
(9015, 9003, 9019, 3, '1'),  -- Sky cardstock
(9016, 9003, 9002, 4, '1'),
-- Crimson Sympathy
(9017, 9004, 9003, 0, '1'),  -- Sympathy Sentiments
(9018, 9004, 9016, 1, '1'),  -- Crimson cardstock
(9019, 9004, 9022, 2, '1'),
(9020, 9004, 9033, 3, '4'),  -- 4 pearl drops
-- Holly Christmas
(9021, 9005, 9010, 0, '1'),  -- Holly & Berries
(9022, 9005, 9013, 1, '1'),  -- Stitched Rectangles
(9023, 9005, 9035, 2, '1'),  -- Christmas Plaid paper
(9024, 9005, 9023, 3, '1'),  -- Kraft cardstock
(9025, 9005, 9004, 4, '1'),  -- Christmas Greetings sentiment
(9026, 9005, 9032, 5, '1'),  -- Sparkle stars
-- Tulip Spring Hello
(9027, 9006, 9040, 0, '1'),  -- New tulip stamps
(9028, 9006, 9024, 1, '1'),  -- Coral cardstock
(9029, 9006, 9017, 2, '1'),  -- Sage cardstock
(9030, 9006, 9022, 3, '1'),
-- Encouragement
(9031, 9007, 9005, 0, '1'),  -- Encouragement Words
(9032, 9007, 9020, 1, '1'),  -- Charcoal cardstock
(9033, 9007, 9022, 2, '1'),
-- Imported sympathy
(9034, 9008, 9003, 0, '1'),
(9035, 9008, 9021, 1, '1');  -- Cream cardstock (out of stock — interesting demo!)

-- -----------------------------------------------------------------------------
--  PROJECT CREATIONS — each row = one time the project was made.
--  Some are pre-script demo data, but the script tells you to ALSO hit
--  "I Made One!" on camera. So leave room — don't over-populate.
-- -----------------------------------------------------------------------------
INSERT INTO project_creations (id, project_id, created_on, notes, materials_used) VALUES
(9001, 9001, datetime('now', '-91 days'), 'First make — gave to Mom''s book club.', NULL),
(9002, 9001, datetime('now', '-60 days'), 'Made 4 for teacher gifts.', NULL),
(9003, 9001, datetime('now', '-22 days'), 'Quick remake — sent to neighbor.', NULL),
(9004, 9002, datetime('now', '-68 days'), 'Birthday card for Aunt Susan.', NULL),
(9005, 9002, datetime('now', '-30 days'), NULL, NULL),
(9006, 9003, datetime('now', '-55 days'), 'For Henry''s 6th birthday.', NULL),
(9007, 9003, datetime('now', '-20 days'), 'For Lily''s 4th birthday.', NULL),
(9008, 9004, datetime('now', '-40 days'), NULL, NULL),
(9009, 9005, datetime('now', '-28 days'), 'Made a batch of 8 for Christmas mailout.', NULL),
(9010, 9005, datetime('now', '-3 days'), 'One more — late one to the Andersons.', NULL),
(9011, 9006, datetime('now', '-8 days'), 'First test card.', NULL),
(9012, 9007, datetime('now', '-4 days'), 'Sent same day to Carol.', NULL),
(9013, 9008, datetime('now', '-12 days'), 'Made Mary''s shared design for the Hendersons.', NULL),
(9014, 9008, datetime('now', '-2 days'), NULL, NULL);

-- -----------------------------------------------------------------------------
--  PROJECT CARD BUILDS — 3 projects get a full build so the "How It Was Made"
--  card is populated and re-opening the Wizard shows pre-filled "Done!" pills.
--
--  state_snapshot is a minimal but valid WizardBuildSnapshot JSON. The wizard
--  reads SelectedCardBase + the per-section objects; the actual steps live in
--  project_card_build_steps below for full fidelity.
-- -----------------------------------------------------------------------------
INSERT INTO project_card_builds (id, project_id, card_base_type, state_snapshot, created_at) VALUES
(9001, 9001, 'A2', '{"Version":"1","SelectedCardBase":"A2","BaseCardstockColor":"Sage","BaseRegularCardstockItemId":9017,"Notes":"Sage on white, banner sentiment."}', datetime('now', '-92 days')),
(9002, 9002, 'A2', '{"Version":"1","SelectedCardBase":"A2","BaseCardstockColor":"Kraft","BaseRegularCardstockItemId":9023,"Notes":"Layered florals — die-cut three colors of cardstock."}', datetime('now', '-70 days')),
(9003, 9005, 'A2', '{"Version":"1","SelectedCardBase":"A2","BaseCardstockColor":"Kraft","BaseRegularCardstockItemId":9023,"Notes":"Plaid frame, holly focal, sparkle stars on red berries."}', datetime('now', '-31 days'));

-- Card build steps. section is "exterior" or "inside"; step_type is snake_case.
-- step_order is per-build. label is human-readable for the How It Was Made card.
INSERT INTO project_card_build_steps (id, build_id, step_order, section, step_type, mat_layer, item_id, cutting_method, label) VALUES
-- Build 9001 (Sage Thank You)
(9001, 9001, 0, 'exterior', 'card_base',         NULL, 9017, NULL, 'Sage cardstock — A2 base, scored at 4¼"'),
(9002, 9001, 1, 'exterior', 'background_mat',    1,    9022, NULL, 'White cardstock mat — trimmed 4 x 5¼"'),
(9003, 9001, 2, 'exterior', 'sentiment',         NULL, 9001, NULL, '"thank you" stamped in Versafine Black'),
(9004, 9001, 3, 'exterior', 'sentiment',         NULL, 9015, 'Custom', 'Sentiment Banners die — banner cut from sage'),
(9005, 9001, 4, 'exterior', 'card_base_adhesive', NULL, NULL, NULL, 'Foam tape behind banner for dimension'),
-- Build 9002 (Cottage Garden Birthday)
(9006, 9002, 0, 'exterior', 'card_base',         NULL, 9023, NULL, 'Kraft cardstock — A2 base'),
(9007, 9002, 1, 'exterior', 'background_mat',    1,    9022, NULL, 'White cardstock mat'),
(9008, 9002, 2, 'exterior', 'focal_decoration_stamp', NULL, 9006, NULL, 'Cottage Garden florals — stamped 3 blooms'),
(9009, 9002, 3, 'exterior', 'focal_decoration',  NULL, 9011, 'Custom', 'Coordinating dies — die-cut each bloom'),
(9010, 9002, 4, 'exterior', 'sentiment',         NULL, 9002, NULL, '"happy birthday" stamped in Salty Ocean'),
(9011, 9002, 5, 'exterior', 'embellishment',     NULL, 9029, NULL, 'Iridescent sequins scattered around focal'),
-- Build 9003 (Holly Christmas)
(9012, 9003, 0, 'exterior', 'card_base',         NULL, 9023, NULL, 'Kraft cardstock — A2 base'),
(9013, 9003, 1, 'exterior', 'background_mat',    1,    9035, NULL, 'Christmas plaid paper mat'),
(9014, 9003, 2, 'exterior', 'focal_mat_piece',   1,    9022, 'Frames', 'White cardstock — Stitched Rectangle die'),
(9015, 9003, 3, 'exterior', 'focal_decoration_stamp', NULL, 9010, NULL, 'Holly & berries stamped in green & red'),
(9016, 9003, 4, 'exterior', 'sentiment',         NULL, 9004, NULL, '"merry christmas" sentiment'),
(9017, 9003, 5, 'exterior', 'embellishment',     NULL, 9032, NULL, 'Sparkle glitter stars on berries');

-- -----------------------------------------------------------------------------
--  WISHLISTS (4 lists, ids 9001..9004) + items (22 rows, ids 9001..9022)
--  "Holiday 2026" is intentionally empty for the empty-state shot.
-- -----------------------------------------------------------------------------
INSERT INTO wishlists (id, name, color, description, created_at) VALUES
(9001, 'Spring Release 2026',  '#E8A4B8', 'Items I want from the TE Spring 2026 release.', datetime('now', '-45 days')),
(9002, 'Birthday Cards',       '#5A9BD4', 'Supplies specifically for birthday card making.', datetime('now', '-120 days')),
(9003, 'Holiday 2026',         '#D45A5A', 'Things I want for Christmas-card season.', datetime('now', '-20 days')),
(9004, 'Someday / Maybe',      '#9A9A9A', 'No rush — toys, big-ticket dies, etc.', datetime('now', '-200 days'));

-- Spring Release 2026 — fullest list, ~5 items, mixed prices
INSERT INTO wishlist_items (id, name, type, item_number, theme, price, image_url, notes, priority, purchased_from, url, wishlist_id, created_at) VALUES
(9001, 'Daffodil Field Stamp Set', 'Stamp', 'TE-STM-2026B', 'Floral', '21.99', 'https://picsum.photos/seed/mcsw9001/300/300', 'Pairs with new dies #2026C', 1, 'Taylored Expressions', 'https://www.tayloredexpressions.com/products/daffodil-field', 9001, datetime('now', '-44 days')),
(9002, 'Daffodil Field Dies', 'Die', 'TE-STM-2026C', 'Floral', '26.99', 'https://picsum.photos/seed/mcsw9002/300/300', NULL, 1, 'Taylored Expressions', 'https://www.tayloredexpressions.com/products/daffodil-field-dies', 9001, datetime('now', '-44 days')),
(9003, 'Spring Pastels Cardstock Pack', 'Cardstock', 'TE-CS-2026A', 'Pastels', '18.99', 'https://picsum.photos/seed/mcsw9003/300/300', '10 sheets, 5 colors', 2, 'Taylored Expressions', 'https://www.tayloredexpressions.com/products/spring-pastels', 9001, datetime('now', '-30 days')),
(9004, 'Watercolor Eggs Stencil', 'Stencil', 'TE-STN-2026A', 'Easter', '14.99', 'https://picsum.photos/seed/mcsw9004/300/300', NULL, 3, 'Taylored Expressions', NULL, 9001, datetime('now', '-22 days')),
(9005, 'Bunny Friends Stamp Set', 'Stamp', 'TE-STM-2026D', 'Easter', '19.99', 'https://picsum.photos/seed/mcsw9005/300/300', 'Cute but maybe wait', 4, 'Taylored Expressions', NULL, 9001, datetime('now', '-10 days'));

-- Birthday Cards — long list
INSERT INTO wishlist_items (id, name, type, item_number, theme, price, image_url, notes, priority, purchased_from, url, wishlist_id, created_at) VALUES
(9006, 'Layering Cake Stamp Set', 'Stamp', 'TE-STM-3001', 'Birthday', '17.99', 'https://picsum.photos/seed/mcsw9006/300/300', 'Multi-layer cake', 1, 'Taylored Expressions', NULL, 9002, datetime('now', '-110 days')),
(9007, 'Birthday Confetti Embellishments', 'Embellishment', 'TE-EMB-3002', 'Birthday', '7.99', 'https://picsum.photos/seed/mcsw9007/300/300', NULL, 1, 'Pretty Pink Posh', NULL, 9002, datetime('now', '-100 days')),
(9008, 'Numbers & Ages Die Set', 'Die', 'TE-DIE-3003', 'Birthday', '24.99', 'https://picsum.photos/seed/mcsw9008/300/300', 'Big numbers — great for milestone birthdays', 2, 'Concord & 9th', NULL, 9002, datetime('now', '-95 days')),
(9009, 'Cake Slice Coordinating Dies', 'Die', 'TE-DIE-3004', 'Birthday', '21.99', 'https://picsum.photos/seed/mcsw9009/300/300', NULL, 2, 'Taylored Expressions', NULL, 9002, datetime('now', '-80 days')),
(9010, 'Birthday Banner Sentiments', 'Stamp', 'TE-STM-3005', 'Birthday', '12.99', 'https://picsum.photos/seed/mcsw9010/300/300', NULL, 3, 'Hero Arts', NULL, 9002, datetime('now', '-40 days')),
(9011, 'Foil Gold Stars Embellishment Pack', 'Embellishment', 'TE-EMB-3006', 'Birthday', '6.99', 'https://picsum.photos/seed/mcsw9011/300/300', NULL, 3, 'Pretty Pink Posh', NULL, 9002, datetime('now', '-25 days')),
(9012, 'Cupcake Border Die', 'Die', 'TE-DIE-3007', 'Birthday', '14.99', 'https://picsum.photos/seed/mcsw9012/300/300', NULL, 4, 'Lawn Fawn', NULL, 9002, datetime('now', '-15 days'));

-- Holiday 2026 — intentionally LEFT EMPTY for empty-state shot

-- Someday / Maybe — big-ticket items
INSERT INTO wishlist_items (id, name, type, item_number, theme, price, image_url, notes, priority, purchased_from, url, wishlist_id, created_at) VALUES
(9013, 'Stamp Platform Pro', 'Tool', 'MISTI-2026', 'Tool', '79.99', 'https://picsum.photos/seed/mcsw9013/300/300', 'The fancy MISTI — saving up', 5, 'Misti', 'https://www.mymisti.com/', 9004, datetime('now', '-180 days')),
(9014, 'Die Cutting Machine Upgrade', 'Tool', 'BIGSHOT-3', 'Tool', '149.99', 'https://picsum.photos/seed/mcsw9014/300/300', 'New Big Shot when current one wears out', 5, 'Sizzix', NULL, 9004, datetime('now', '-160 days')),
(9015, 'Watercolor Brush Pen Set', 'Tool', 'TOMBOW-12', 'Tool', '34.99', 'https://picsum.photos/seed/mcsw9015/300/300', '12-color set', 4, 'Tombow', NULL, 9004, datetime('now', '-90 days')),
(9016, 'Light Pad — A4', 'Tool', 'LP-A4', 'Tool', '49.99', 'https://picsum.photos/seed/mcsw9016/300/300', 'For tracing — would be nice', 5, 'Amazon', NULL, 9004, datetime('now', '-60 days')),
(9017, 'Heat Embossing Starter Kit', 'Tool', 'EMB-START', 'Tool', '29.99', 'https://picsum.photos/seed/mcsw9017/300/300', NULL, 4, 'Ranger', NULL, 9004, datetime('now', '-30 days')),
(9018, 'Acrylic Stamp Block Set', 'Tool', 'STMP-BLK-5', 'Tool', '19.99', 'https://picsum.photos/seed/mcsw9018/300/300', '5 sizes', 3, 'Taylored Expressions', NULL, 9004, datetime('now', '-25 days')),
(9019, 'Specialty Inks — Metallic Set', 'Ink', 'TE-INK-METAL', 'Basics', '27.99', 'https://picsum.photos/seed/mcsw9019/300/300', 'Gold, silver, copper, rose gold', 3, 'Taylored Expressions', NULL, 9004, datetime('now', '-12 days')),
(9020, 'Glassine Envelopes — 100ct', 'Tool', 'GLAS-100', 'Tool', '14.99', 'https://picsum.photos/seed/mcsw9020/300/300', NULL, 2, 'Amazon', NULL, 9004, datetime('now', '-8 days')),
(9021, 'Adhesive Foam Squares Bulk Pack', 'Tool', 'FOAM-BLK', 'Tool', '11.99', 'https://picsum.photos/seed/mcsw9021/300/300', 'Always running out', 1, 'Scrapbook.com', NULL, 9004, datetime('now', '-5 days')),
(9022, 'Glitter Brush Markers — Sparkle Set', 'Tool', 'SPARK-BR', 'Tool', '22.99', 'https://picsum.photos/seed/mcsw9022/300/300', NULL, 4, 'Studio Katia', NULL, 9004, datetime('now', '-2 days'));

-- -----------------------------------------------------------------------------
--  ADDRESS BOOK (8 entries) — for the Address Book demo
-- -----------------------------------------------------------------------------
INSERT INTO address_book (id, first_name, last_name, address_line1, address_line2, city, state, zip_code, country, phone, email, notes, created_at) VALUES
(9001, 'Mary', 'Henderson', '142 Oak Street', NULL, 'Madison', 'WI', '53703', 'USA', NULL, 'mary.h@example.com', 'Crafting circle — birthday Jan 12', datetime('now', '-300 days')),
(9002, 'Carol', 'Whitman', '88 Birch Lane', 'Apt 3B', 'Minneapolis', 'MN', '55401', 'USA', NULL, NULL, 'Co-worker', datetime('now', '-280 days')),
(9003, 'Susan', 'Reyes', '410 Pine Court', NULL, 'Portland', 'OR', '97205', 'USA', NULL, 'sreyes@example.com', 'Aunt — Christmas list', datetime('now', '-250 days')),
(9004, 'Tom & Linda', 'Anderson', '12 Maple Drive', NULL, 'Boulder', 'CO', '80302', 'USA', NULL, NULL, 'Anniversary in July', datetime('now', '-200 days')),
(9005, 'Henry', 'Park', '7 Cedar Way', NULL, 'Seattle', 'WA', '98101', 'USA', NULL, NULL, 'Nephew — turned 6', datetime('now', '-150 days')),
(9006, 'Lily', 'Park', '7 Cedar Way', NULL, 'Seattle', 'WA', '98101', 'USA', NULL, NULL, 'Niece — turned 4', datetime('now', '-150 days')),
(9007, 'Mrs. Patterson', NULL, '305 Elm Avenue', NULL, 'Madison', 'WI', '53703', 'USA', NULL, NULL, 'Henry''s teacher', datetime('now', '-90 days')),
(9008, 'Mary', 'K.', '92 Cottage Lane', NULL, 'Madison', 'WI', '53703', 'USA', NULL, NULL, 'Crafting circle — shared the sympathy card design', datetime('now', '-15 days'));

-- -----------------------------------------------------------------------------
--  CALENDAR EVENTS — birthdays + reminders to make cards
-- -----------------------------------------------------------------------------
INSERT INTO calendar_events (id, title, description, event_date, event_time, is_all_day, reminder_minutes_before, color, reminder_dismissed, created_at) VALUES
(9001, 'Susan''s Birthday — make card', 'Aunt Susan — try the Cottage Garden Birthday design', date('now', '+5 days'), NULL, 1, 4320, '#E8A4B8', 0, datetime('now', '-30 days')),
(9002, 'Anniversary — Tom & Linda', 'Anniversary card to Andersons', date('now', '+18 days'), NULL, 1, 4320, '#5A9BD4', 0, datetime('now', '-30 days')),
(9003, 'Crafting circle meetup', 'Monthly meetup at Mary''s house', date('now', '+9 days'), '14:00', 0, 60, '#9A9A9A', 0, datetime('now', '-14 days')),
(9004, 'TE Spring Release drops', 'Watch for the daffodil set!', date('now', '-3 days'), NULL, 1, 1440, '#D45A5A', 0, datetime('now', '-30 days')),
(9005, 'Order more cream cardstock', 'Out of stock — needed for sympathy cards', date('now', '+2 days'), NULL, 1, 1440, '#D45A5A', 0, datetime('now', '-2 days')),
(9006, 'Henry''s birthday', 'Make Forest Friends Birthday card', date('now', '+45 days'), NULL, 1, 10080, '#F2A03D', 0, datetime('now', '-60 days'));

-- -----------------------------------------------------------------------------
--  INSPIRATION BOARDS + IMAGES — populates the Inspiration view
-- -----------------------------------------------------------------------------
INSERT INTO inspiration_boards (id, name, description, parent_board_id, display_order, default_types, default_themes, default_colors, default_sentiment, default_te_colors, created_at) VALUES
(9001, 'Spring Inspiration', 'Pastel florals and bright spring palettes', NULL, 0, 'Stamp,Die', 'Floral', 'Pink,Green,Yellow', NULL, NULL, datetime('now', '-100 days')),
(9002, 'Clean & Simple', 'Minimalist card designs', NULL, 1, 'Stamp', NULL, 'White,Black', NULL, NULL, datetime('now', '-90 days')),
(9003, 'Christmas Ideas', 'Plaid, foil, traditional palettes', NULL, 2, 'Stamp,Paper', 'Christmas', 'Red,Green,Gold', NULL, NULL, datetime('now', '-80 days'));

INSERT INTO inspiration_images (id, image_url, title, notes, board_id, color, types, theme, sentiment, created_at) VALUES
(9001, 'https://picsum.photos/seed/mcsi9001/600/600', 'Tulip layered', 'Layer of die-cut tulips on kraft', 9001, 'Pink', 'Stamp,Die', 'Floral', 'Hello Spring', datetime('now', '-90 days')),
(9002, 'https://picsum.photos/seed/mcsi9002/600/600', 'Wildflower meadow', NULL, 9001, 'Yellow', 'Stamp', 'Floral', NULL, datetime('now', '-85 days')),
(9003, 'https://picsum.photos/seed/mcsi9003/600/600', 'Watercolor poppies', 'Wet-on-wet wash', 9001, 'Red', 'Stamp', 'Floral', NULL, datetime('now', '-70 days')),
(9004, 'https://picsum.photos/seed/mcsi9004/600/600', 'Black & white thank you', NULL, 9002, 'Black', 'Stamp', NULL, 'thank you', datetime('now', '-60 days')),
(9005, 'https://picsum.photos/seed/mcsi9005/600/600', 'One-layer hello', NULL, 9002, 'White', 'Stamp', NULL, 'hello', datetime('now', '-55 days')),
(9006, 'https://picsum.photos/seed/mcsi9006/600/600', 'Minimal birthday', NULL, 9002, 'Gray', 'Stamp', 'Birthday', 'happy birthday', datetime('now', '-50 days')),
(9007, 'https://picsum.photos/seed/mcsi9007/600/600', 'Plaid Christmas tree', NULL, 9003, 'Red', 'Paper', 'Christmas', NULL, datetime('now', '-40 days')),
(9008, 'https://picsum.photos/seed/mcsi9008/600/600', 'Foil holly border', NULL, 9003, 'Gold', 'Stamp', 'Christmas', NULL, datetime('now', '-35 days')),
(9009, 'https://picsum.photos/seed/mcsi9009/600/600', 'Snowflake monochrome', NULL, 9003, 'White', 'Stencil', 'Christmas', NULL, datetime('now', '-30 days')),
(9010, 'https://picsum.photos/seed/mcsi9010/600/600', 'Vintage Santa', NULL, 9003, 'Red', 'Stamp', 'Christmas', NULL, datetime('now', '-25 days')),
(9011, 'https://picsum.photos/seed/mcsi9011/600/600', 'Sage & white CAS', NULL, 9002, 'Green', 'Stamp', NULL, 'thank you', datetime('now', '-20 days')),
(9012, 'https://picsum.photos/seed/mcsi9012/600/600', 'Spring confetti', NULL, 9001, 'Pink', 'Embellishment', 'Floral', NULL, datetime('now', '-15 days'));

COMMIT;

-- =============================================================================
-- Quick sanity counts — uncomment to verify after running:
--   SELECT 'items'           AS t, COUNT(*) FROM items           WHERE id >= 9000
--   UNION ALL SELECT 'projects',           COUNT(*) FROM projects           WHERE id >= 9000
--   UNION ALL SELECT 'project_creations',  COUNT(*) FROM project_creations  WHERE id >= 9000
--   UNION ALL SELECT 'project_card_builds',COUNT(*) FROM project_card_builds WHERE id >= 9000
--   UNION ALL SELECT 'wishlist_items',     COUNT(*) FROM wishlist_items     WHERE id >= 9000;
-- =============================================================================
