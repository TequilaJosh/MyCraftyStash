-- Calendar Events table for the Social > Calendar feature
-- Run this script in SQL Server Management Studio

CREATE TABLE calendar_events (
    id                      INT IDENTITY(1,1) PRIMARY KEY,
    title                   NVARCHAR(255)  NOT NULL,
    description             NVARCHAR(MAX)  NULL,
    event_date              DATE           NOT NULL,
    event_time              TIME           NULL,          -- NULL = all-day event
    is_all_day              BIT            NOT NULL DEFAULT 1,
    reminder_minutes_before INT            NOT NULL DEFAULT 1440, -- 1440 = 1 day
    color                   NVARCHAR(50)   NULL DEFAULT '#D61F26',
    reminder_dismissed      BIT            NOT NULL DEFAULT 0,
    created_at              DATETIME2      NOT NULL DEFAULT GETDATE(),
    updated_at              DATETIME2      NULL
);

-- Index for fast month-range queries used by the calendar grid
CREATE INDEX IX_calendar_events_event_date ON calendar_events (event_date);

-- Index for the startup reminder check
CREATE INDEX IX_calendar_events_reminder
    ON calendar_events (reminder_dismissed, event_date)
    INCLUDE (title, reminder_minutes_before, event_time, is_all_day);

PRINT 'Calendar events table created successfully!';
