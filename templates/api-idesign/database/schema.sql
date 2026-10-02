-- Local development schema for docker compose.
-- Placeholder until the Liquibase template owns schema changes.

CREATE TABLE IF NOT EXISTS orders (
    id              uuid            PRIMARY KEY,
    customer_name   varchar(200)    NOT NULL,
    subtotal        numeric(18, 2)  NOT NULL,
    discount        numeric(18, 2)  NOT NULL,
    total           numeric(18, 2)  NOT NULL,
    created_at      timestamptz     NOT NULL
);

CREATE TABLE IF NOT EXISTS order_lines (
    order_id        uuid            NOT NULL REFERENCES orders (id) ON DELETE CASCADE,
    line_number     int             NOT NULL,
    sku             varchar(64)     NOT NULL,
    quantity        int             NOT NULL,
    unit_price      numeric(18, 2)  NOT NULL,
    line_total      numeric(18, 2)  NOT NULL,
    PRIMARY KEY (order_id, line_number)
);

CREATE INDEX IF NOT EXISTS ix_orders_created_at ON orders (created_at DESC);
