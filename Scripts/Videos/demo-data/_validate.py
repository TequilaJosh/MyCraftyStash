"""
Syntax/integrity check for SeedDemoData.sql and WipeDemoData.sql.

Builds the same tables EF Core's migrations build (best-effort, derived from the
migration files), then runs the seed and the wipe against a :memory: DB.
This is a smoke test only — it won't catch every schema drift, but it WILL
catch: SQL syntax errors, column-name typos, NOT NULL violations, FK violations
between rows in the seed itself, and ordering issues in the wipe.

Run with:  python _validate.py
"""
from __future__ import annotations
import sqlite3
import sys
from pathlib import Path

HERE = Path(__file__).parent
SEED = HERE / "SeedDemoData.sql"
WIPE = HERE / "WipeDemoData.sql"

# Schema mirrors EF Core's snake_case columns. Kept loose on lengths because
# SQLite ignores VARCHAR(n) length constraints anyway.
SCHEMA = """
CREATE TABLE items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    type TEXT NOT NULL,
    location TEXT,
    theme TEXT,
    sentiments TEXT,
    image_url TEXT,
    price TEXT,
    date_purchased TEXT,
    item_number TEXT,
    is_discontinued INTEGER NOT NULL DEFAULT 0,
    subtype TEXT,
    stencil_layers INTEGER,
    pack_size INTEGER,
    current_stock INTEGER,
    purchased_from TEXT,
    notes TEXT,
    site_url TEXT,
    created_at TEXT NOT NULL
);

CREATE TABLE projects (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    description TEXT,
    image_url TEXT,
    technique TEXT,
    notes TEXT,
    created_at TEXT NOT NULL,
    is_shared INTEGER NOT NULL DEFAULT 0,
    shared_at TEXT,
    shared_from_name TEXT
);

CREATE TABLE item_images (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    item_id INTEGER NOT NULL REFERENCES items(id) ON DELETE CASCADE,
    image_url TEXT NOT NULL,
    sort_order INTEGER NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE project_images (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    image_url TEXT NOT NULL,
    sort_order INTEGER NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE project_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    item_id INTEGER NOT NULL REFERENCES items(id) ON DELETE CASCADE,
    sort_order INTEGER NOT NULL,
    amount_used_per_creation TEXT
);

CREATE TABLE item_relationships (
    item_id INTEGER NOT NULL REFERENCES items(id) ON DELETE CASCADE,
    related_item_id INTEGER NOT NULL REFERENCES items(id) ON DELETE CASCADE,
    PRIMARY KEY (item_id, related_item_id)
);

CREATE TABLE item_purchases (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    item_id INTEGER NOT NULL REFERENCES items(id) ON DELETE CASCADE,
    quantity INTEGER NOT NULL,
    price_per_item NUMERIC NOT NULL,
    date_purchased TEXT,
    created_at TEXT NOT NULL
);

CREATE TABLE project_creations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    created_on TEXT NOT NULL,
    notes TEXT,
    materials_used TEXT
);

CREATE TABLE stacklet_dies (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    item_id INTEGER NOT NULL REFERENCES items(id) ON DELETE CASCADE,
    die_number TEXT,
    width NUMERIC,
    height NUMERIC,
    label TEXT
);

CREATE TABLE project_card_builds (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    card_base_type TEXT NOT NULL,
    state_snapshot TEXT,
    created_at TEXT NOT NULL
);

CREATE TABLE project_card_build_steps (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    build_id INTEGER NOT NULL REFERENCES project_card_builds(id) ON DELETE CASCADE,
    step_order INTEGER NOT NULL,
    section TEXT NOT NULL,
    step_type TEXT NOT NULL,
    mat_layer INTEGER,
    item_id INTEGER REFERENCES items(id) ON DELETE SET NULL,
    stacklet_die_id INTEGER REFERENCES stacklet_dies(id) ON DELETE SET NULL,
    cutting_method TEXT,
    label TEXT NOT NULL
);

CREATE TABLE wishlists (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    color TEXT,
    description TEXT,
    created_at TEXT NOT NULL
);

CREATE TABLE wishlist_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    type TEXT,
    item_number TEXT,
    theme TEXT,
    price TEXT,
    image_url TEXT,
    notes TEXT,
    priority INTEGER NOT NULL,
    purchased_from TEXT,
    url TEXT,
    wishlist_id INTEGER REFERENCES wishlists(id) ON DELETE SET NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE address_book (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    first_name TEXT NOT NULL,
    last_name TEXT,
    address_line1 TEXT,
    address_line2 TEXT,
    city TEXT,
    state TEXT,
    zip_code TEXT,
    country TEXT,
    phone TEXT,
    email TEXT,
    notes TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT
);

CREATE TABLE calendar_events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    description TEXT,
    event_date TEXT NOT NULL,
    event_time TEXT,
    reminder_minutes_before INTEGER NOT NULL,
    color TEXT,
    is_all_day INTEGER NOT NULL,
    reminder_dismissed INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT
);

CREATE TABLE sentiment_images (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    item_id INTEGER NOT NULL REFERENCES items(id) ON DELETE CASCADE,
    image_data TEXT NOT NULL,
    extracted_text TEXT NOT NULL,
    search_text TEXT NOT NULL,
    sort_order INTEGER NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE inspiration_boards (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    description TEXT,
    parent_board_id INTEGER REFERENCES inspiration_boards(id) ON DELETE RESTRICT,
    created_at TEXT NOT NULL,
    display_order INTEGER NOT NULL,
    default_types TEXT,
    default_themes TEXT,
    default_colors TEXT,
    default_sentiment TEXT,
    default_te_colors TEXT,
    DefaultSubtypes TEXT,
    DefaultItemIds TEXT
);

CREATE TABLE inspiration_images (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    image_url TEXT NOT NULL,
    title TEXT,
    notes TEXT,
    created_at TEXT NOT NULL,
    board_id INTEGER REFERENCES inspiration_boards(id) ON DELETE SET NULL,
    color TEXT,
    types TEXT,
    theme TEXT,
    sentiment TEXT,
    te_color TEXT
);

CREATE TABLE inspiration_image_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    inspiration_image_id INTEGER NOT NULL REFERENCES inspiration_images(id) ON DELETE CASCADE,
    item_id INTEGER NOT NULL REFERENCES items(id) ON DELETE CASCADE
);

CREATE TABLE hidden_inspiration_images (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    image_key TEXT NOT NULL,
    created_at TEXT NOT NULL
);
"""

EXPECTED_COUNTS = {
    "items": 40,
    "projects": 8,
    "project_images": 16,
    "project_items": 35,
    "project_creations": 14,
    "project_card_builds": 3,
    "project_card_build_steps": 17,
    "wishlists": 4,
    "wishlist_items": 22,
    "address_book": 8,
    "calendar_events": 6,
    "sentiment_images": 6,
    "inspiration_boards": 3,
    "inspiration_images": 12,
    "item_relationships": 8,
    "item_purchases": 20,
}


def main() -> int:
    conn = sqlite3.connect(":memory:")
    conn.execute("PRAGMA foreign_keys = ON;")
    conn.executescript(SCHEMA)

    seed_sql = SEED.read_text(encoding="utf-8")
    try:
        conn.executescript(seed_sql)
    except sqlite3.Error as e:
        print(f"[FAIL] Seed SQL error: {e}", file=sys.stderr)
        return 1

    # Verify foreign keys are satisfied
    fk_violations = conn.execute("PRAGMA foreign_key_check").fetchall()
    if fk_violations:
        print(f"[FAIL] FK violations after seed: {fk_violations}", file=sys.stderr)
        return 1

    # Verify counts
    failures = []
    for table, expected in EXPECTED_COUNTS.items():
        actual = conn.execute(f"SELECT COUNT(*) FROM {table} WHERE rowid > 0").fetchone()[0]
        # For item_relationships we don't have id but rowid still works
        marker = "OK " if actual == expected else "FAIL"
        line = f"  {marker}  {table:<28} expected={expected:>4}  got={actual:>4}"
        print(line)
        if actual != expected:
            failures.append(table)

    if failures:
        print(f"[FAIL] Row count mismatch in: {failures}", file=sys.stderr)
        return 1

    # Verify wipe removes everything we seeded
    wipe_sql = WIPE.read_text(encoding="utf-8")
    try:
        conn.executescript(wipe_sql)
    except sqlite3.Error as e:
        print(f"[FAIL] Wipe SQL error: {e}", file=sys.stderr)
        return 1

    fk_violations = conn.execute("PRAGMA foreign_key_check").fetchall()
    if fk_violations:
        print(f"[FAIL] FK violations after wipe: {fk_violations}", file=sys.stderr)
        return 1

    remaining_failures = []
    for table in EXPECTED_COUNTS:
        actual = conn.execute(f"SELECT COUNT(*) FROM {table} WHERE rowid > 0").fetchone()[0]
        if actual > 0:
            print(f"  FAIL  {table} still has {actual} rows after wipe", file=sys.stderr)
            remaining_failures.append(table)
    if remaining_failures:
        return 1

    print("\n[OK] Seed applied, all FKs valid, counts match, wipe clean.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
