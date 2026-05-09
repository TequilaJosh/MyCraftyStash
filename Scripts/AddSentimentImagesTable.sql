-- Sentiment Images table for storing extracted sentiment snippets with OCR text
-- Run this script in SQL Server Management Studio to add the sentiment_images table

CREATE TABLE sentiment_images (
    id INT IDENTITY(1,1) PRIMARY KEY,
    item_id INT NOT NULL,
    image_data NVARCHAR(MAX) NOT NULL,
    extracted_text NVARCHAR(MAX) NOT NULL,
    search_text NVARCHAR(MAX) NOT NULL,
    sort_order INT NOT NULL DEFAULT 0,
    created_at DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_sentiment_images_items FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE
);

CREATE INDEX IX_sentiment_images_item_id ON sentiment_images(item_id);
-- Note: search_text is NVARCHAR(MAX) and cannot have a regular index
-- The application uses LIKE queries for sentiment text searching

PRINT 'Sentiment images table created successfully!';
