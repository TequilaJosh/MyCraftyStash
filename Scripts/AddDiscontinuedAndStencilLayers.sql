-- Add is_discontinued and stencil_layers columns to the items table
-- Run this script in SQL Server Management Studio

-- Add is_discontinued column (default false)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'items' AND COLUMN_NAME = 'is_discontinued')
BEGIN
    ALTER TABLE items ADD is_discontinued BIT NOT NULL DEFAULT 0;
    PRINT 'Added is_discontinued column to items table';
END
ELSE
BEGIN
    PRINT 'is_discontinued column already exists';
END

-- Add stencil_layers column (nullable integer)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'items' AND COLUMN_NAME = 'stencil_layers')
BEGIN
    ALTER TABLE items ADD stencil_layers INT NULL;
    PRINT 'Added stencil_layers column to items table';
END
ELSE
BEGIN
    PRINT 'stencil_layers column already exists';
END

PRINT 'Migration completed successfully!';
