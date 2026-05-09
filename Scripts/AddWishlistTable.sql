-- Run this on your JandHGreetings SQL Server database
-- Adds the wishlist_items table for the Wish List feature

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'wishlist_items')
BEGIN
    CREATE TABLE wishlist_items (
        id             INT IDENTITY(1,1) PRIMARY KEY,
        name           NVARCHAR(255) NOT NULL,
        type           NVARCHAR(100) NULL,
        item_number    NVARCHAR(100) NULL,
        theme          NVARCHAR(255) NULL,
        price          DECIMAL(10,2) NULL,
        image_url      NVARCHAR(MAX) NULL,
        notes          NVARCHAR(MAX) NULL,
        priority       INT NOT NULL DEFAULT 1,
        purchased_from NVARCHAR(255) NULL,
        url            NVARCHAR(1000) NULL,
        created_at     DATETIME2 NOT NULL DEFAULT GETDATE()
    );
    PRINT 'wishlist_items table created.';
END
ELSE
BEGIN
    PRINT 'wishlist_items table already exists - no changes made.';
END
