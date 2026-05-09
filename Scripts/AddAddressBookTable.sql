-- Address Book table for storing contacts with addresses and notes
-- Run this script in SQL Server Management Studio to add the address_book table

CREATE TABLE address_book (
    id              INT IDENTITY(1,1) PRIMARY KEY,
    first_name      NVARCHAR(100) NOT NULL,
    last_name       NVARCHAR(100) NULL,
    address_line1   NVARCHAR(255) NULL,
    address_line2   NVARCHAR(255) NULL,
    city            NVARCHAR(100) NULL,
    state           NVARCHAR(100) NULL,
    zip_code        NVARCHAR(20)  NULL,
    country         NVARCHAR(100) NULL,
    phone           NVARCHAR(50)  NULL,
    email           NVARCHAR(255) NULL,
    notes           NVARCHAR(MAX) NULL,
    created_at      DATETIME2     NOT NULL DEFAULT GETDATE(),
    updated_at      DATETIME2     NULL
);

-- Index for fast name lookups and sorting
CREATE INDEX IX_address_book_last_name  ON address_book (last_name, first_name);
CREATE INDEX IX_address_book_first_name ON address_book (first_name);

-- Optional: full-text search support (requires Full-Text Search feature installed)
-- CREATE FULLTEXT CATALOG AddressBookCatalog AS DEFAULT;
-- CREATE FULLTEXT INDEX ON address_book (first_name, last_name, city, state, notes)
--     KEY INDEX PK__address_book__... ON AddressBookCatalog;

PRINT 'Address book table created successfully!';
