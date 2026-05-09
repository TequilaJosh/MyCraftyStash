-- Migration: Add purchased_from and notes to items, technique and notes to projects
-- Run this once against your Azure SQL database

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('items') AND name = 'purchased_from')
    ALTER TABLE items ADD purchased_from NVARCHAR(255) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('items') AND name = 'notes')
    ALTER TABLE items ADD notes NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('projects') AND name = 'technique')
    ALTER TABLE projects ADD technique NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('projects') AND name = 'notes')
    ALTER TABLE projects ADD notes NVARCHAR(MAX) NULL;
