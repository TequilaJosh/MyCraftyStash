-- SQL Server Database Schema for My Crafty Stash
-- Run this script in SQL Server Management Studio to create the database and tables
-- Compatible with both Web Application and WPF Desktop Application
-- WARNING: This script DROPS existing tables and recreates them (data will be lost)

-- Create database (uncomment if needed)
-- CREATE DATABASE MyCraftyStash;
-- GO
-- USE MyCraftyStash;
-- GO

-- ============================================
-- DROP EXISTING TABLES
-- Drop in reverse order of dependencies (child tables first)
-- ============================================
IF OBJECT_ID('dbo.sentiment_images', 'U') IS NOT NULL DROP TABLE dbo.sentiment_images;
IF OBJECT_ID('dbo.item_purchases', 'U') IS NOT NULL DROP TABLE dbo.item_purchases;
IF OBJECT_ID('dbo.item_relationships', 'U') IS NOT NULL DROP TABLE dbo.item_relationships;
IF OBJECT_ID('dbo.project_items', 'U') IS NOT NULL DROP TABLE dbo.project_items;
IF OBJECT_ID('dbo.project_images', 'U') IS NOT NULL DROP TABLE dbo.project_images;
IF OBJECT_ID('dbo.item_images', 'U') IS NOT NULL DROP TABLE dbo.item_images;
IF OBJECT_ID('dbo.inspiration_images', 'U') IS NOT NULL DROP TABLE dbo.inspiration_images;
IF OBJECT_ID('dbo.projects', 'U') IS NOT NULL DROP TABLE dbo.projects;
IF OBJECT_ID('dbo.items', 'U') IS NOT NULL DROP TABLE dbo.items;

PRINT 'Existing tables dropped.';

-- ============================================
-- ITEMS TABLE
-- Core inventory items (stamps, dies, stencils, etc.)
-- ============================================
CREATE TABLE items (
    id INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(255) NOT NULL,
    type NVARCHAR(100) NOT NULL,
    location NVARCHAR(255) NULL,
    theme NVARCHAR(255) NULL,
    sentiments NVARCHAR(MAX) NULL,
    image_url NVARCHAR(MAX) NULL,
    price DECIMAL(10,2) NULL,
    date_purchased DATE NULL,
    item_number NVARCHAR(100) NULL,
    is_discontinued BIT NOT NULL DEFAULT 0,
    stencil_layers INT NULL,
    created_at DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- ============================================
-- ITEM IMAGES TABLEma
-- Multiple images per item for detailed documentation
-- ============================================
CREATE TABLE item_images (
    id INT IDENTITY(1,1) PRIMARY KEY,
    item_id INT NOT NULL,
    image_url NVARCHAR(MAX) NOT NULL,
    sort_order INT NOT NULL DEFAULT 0,
    created_at DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_item_images_items FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE
);

-- ============================================
-- PROJECTS TABLE
-- Completed craft projects
-- ============================================
CREATE TABLE projects (
    id INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(255) NOT NULL,
    description NVARCHAR(MAX) NULL,
    image_url NVARCHAR(MAX) NULL,
    created_at DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- ============================================
-- PROJECT IMAGES TABLE
-- Multiple images per project
-- ============================================
CREATE TABLE project_images (
    id INT IDENTITY(1,1) PRIMARY KEY,
    project_id INT NOT NULL,
    image_url NVARCHAR(MAX) NOT NULL,
    sort_order INT NOT NULL DEFAULT 0,
    created_at DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_project_images_projects FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE
);

-- ============================================
-- PROJECT ITEMS TABLE
-- Junction table linking projects to items used
-- ============================================
CREATE TABLE project_items (
    project_id INT NOT NULL,
    item_id INT NOT NULL,
    PRIMARY KEY (project_id, item_id),
    CONSTRAINT FK_project_items_projects FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    CONSTRAINT FK_project_items_items FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE
);

-- ============================================
-- ITEM RELATIONSHIPS TABLE
-- Self-referential junction for related items (e.g., matching stamp/die sets)
-- Note: SQL Server doesn't allow multiple cascade paths, so related_item_id uses NO ACTION
-- The application handles cleanup of reverse relationships before deleting items
-- ============================================
CREATE TABLE item_relationships (
    item_id INT NOT NULL,
    related_item_id INT NOT NULL,
    PRIMARY KEY (item_id, related_item_id),
    CONSTRAINT FK_item_relationships_items FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE,
    CONSTRAINT FK_item_relationships_related FOREIGN KEY (related_item_id) REFERENCES items(id) ON DELETE NO ACTION
);

-- ============================================
-- ITEM PURCHASES TABLE
-- Purchase history tracking (quantity, price per item, date)
-- Allows tracking multiple purchases of the same item over time
-- ============================================
CREATE TABLE item_purchases (
    id INT IDENTITY(1,1) PRIMARY KEY,
    item_id INT NOT NULL,
    quantity INT NOT NULL DEFAULT 1,
    price_per_item DECIMAL(10,2) NOT NULL DEFAULT 0,
    date_purchased DATE NULL,
    created_at DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_item_purchases_items FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE
);

-- ============================================
-- INSPIRATION IMAGES TABLE
-- Gallery for storing inspiration images with notes
-- ============================================
CREATE TABLE inspiration_images (
    id INT IDENTITY(1,1) PRIMARY KEY,
    image_url NVARCHAR(MAX) NOT NULL,
    title NVARCHAR(255) NULL,
    notes NVARCHAR(MAX) NULL,
    created_at DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- ============================================
-- SENTIMENT IMAGES TABLE (WPF Desktop App Only)
-- Stores individual sentiment snippets extracted from stamp images
-- with OCR text for searchable sentiment database
-- ============================================
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

-- ============================================
-- INDEXES
-- For better query performance
-- ============================================
CREATE INDEX IX_items_type ON items(type);
CREATE INDEX IX_items_theme ON items(theme);
CREATE INDEX IX_items_location ON items(location);
CREATE INDEX IX_items_is_discontinued ON items(is_discontinued);
CREATE INDEX IX_items_item_number ON items(item_number);
CREATE INDEX IX_item_images_item_id ON item_images(item_id);
CREATE INDEX IX_project_images_project_id ON project_images(project_id);
CREATE INDEX IX_project_items_item_id ON project_items(item_id);
CREATE INDEX IX_item_relationships_related_item_id ON item_relationships(related_item_id);
CREATE INDEX IX_item_purchases_item_id ON item_purchases(item_id);
CREATE INDEX IX_item_purchases_date ON item_purchases(date_purchased);
CREATE INDEX IX_sentiment_images_item_id ON sentiment_images(item_id);
-- Note: search_text is NVARCHAR(MAX) and cannot have a regular index
-- The application uses LIKE queries for sentiment text searching

PRINT '';
PRINT 'Database schema created successfully!';
PRINT '';
PRINT 'Tables created:';
PRINT '  - items: Core inventory items (stamps, dies, stencils, etc.)';
PRINT '  - item_images: Multiple images per item';
PRINT '  - projects: Completed craft projects';
PRINT '  - project_images: Multiple images per project';
PRINT '  - project_items: Links projects to items used';
PRINT '  - item_relationships: Links related items together';
PRINT '  - item_purchases: Purchase history tracking';
PRINT '  - inspiration_images: Inspiration gallery';
PRINT '  - sentiment_images: OCR-searchable sentiment snippets (WPF only)';
