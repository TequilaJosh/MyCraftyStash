-- Create item_purchases table if it doesn't exist
-- Run this on your SQL Server database

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'item_purchases')
BEGIN
    CREATE TABLE item_purchases (
        id INT IDENTITY(1,1) PRIMARY KEY,
        item_id INT NOT NULL,
        quantity INT NOT NULL DEFAULT 1,
        price_per_item DECIMAL(12,2) NOT NULL DEFAULT 0,
        date_purchased DATE NULL,
        created_at DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_item_purchases_items FOREIGN KEY (item_id) 
            REFERENCES items(id) ON DELETE CASCADE
    );
    
    CREATE INDEX IX_item_purchases_item_id ON item_purchases(item_id);
    
    PRINT 'item_purchases table created successfully.';
END
ELSE
BEGIN
    PRINT 'item_purchases table already exists.';
END
