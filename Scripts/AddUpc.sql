-- Add upc column to the items table
-- Stores the UPC barcode number for scanning via the web viewer
-- Run this script in SQL Server Management Studio or Azure Data Studio

IF NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'items' AND COLUMN_NAME = 'upc'
)
BEGIN
    ALTER TABLE items ADD upc NVARCHAR(30) NULL;
    PRINT 'Added upc column to items table';
END
ELSE
BEGIN
    PRINT 'upc column already exists - no changes made';
END

PRINT 'Migration completed successfully!';
