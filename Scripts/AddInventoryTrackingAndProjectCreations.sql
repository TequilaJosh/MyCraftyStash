-- ============================================================
-- Migration: Inventory Tracking + Project Creations
-- Run once to add pack_size, current_stock, amount_used_per_creation,
-- and the project_creations table.
-- ============================================================

-- 1. pack_size column on items
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'items' AND COLUMN_NAME = 'pack_size'
)
BEGIN
    ALTER TABLE items ADD pack_size INT NULL;
    PRINT 'Column pack_size added to items.';
END
ELSE
    PRINT 'pack_size already exists - skipped.';

-- 2. current_stock column on items
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'items' AND COLUMN_NAME = 'current_stock'
)
BEGIN
    ALTER TABLE items ADD current_stock INT NULL;
    PRINT 'Column current_stock added to items.';
END
ELSE
    PRINT 'current_stock already exists - skipped.';

-- 3. amount_used_per_creation column on project_items
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'project_items' AND COLUMN_NAME = 'amount_used_per_creation'
)
BEGIN
    ALTER TABLE project_items ADD amount_used_per_creation DECIMAL(10,4) NULL;
    PRINT 'Column amount_used_per_creation added to project_items.';
END
ELSE
    PRINT 'amount_used_per_creation already exists - skipped.';

-- 4. project_creations table
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'project_creations'
)
BEGIN
    CREATE TABLE project_creations (
        Id              INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
        ProjectId       INT            NOT NULL,
        CreatedOn       DATETIME2      NOT NULL DEFAULT GETDATE(),
        Notes           NVARCHAR(500)  NULL,
        MaterialsUsed   NVARCHAR(MAX)  NULL,   -- JSON audit log of deductions
        CONSTRAINT FK_project_creations_projects
            FOREIGN KEY (ProjectId) REFERENCES projects(Id) ON DELETE CASCADE
    );
    PRINT 'Table project_creations created.';
END
ELSE
    PRINT 'project_creations already exists - skipped.';
