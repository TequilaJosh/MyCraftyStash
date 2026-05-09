-- Add subtype column to items table
-- Run this script once to add subtype support

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'items' AND COLUMN_NAME = 'subtype'
)
BEGIN
    ALTER TABLE items ADD subtype NVARCHAR(255) NULL;
    PRINT 'Column subtype added to items table.';
END
ELSE
BEGIN
    PRINT 'Column subtype already exists in items table.';
END
