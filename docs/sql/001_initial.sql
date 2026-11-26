CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE transfers (
    "Id" uuid NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" character(3) NOT NULL,
    "Description" character varying(500) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "Category" character varying(30),
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_transfers" PRIMARY KEY ("Id")
);

CREATE INDEX ix_transfers_created_at ON transfers ("CreatedAt");

CREATE INDEX ix_transfers_status ON transfers ("Status");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261117213440_InitialCreate', '10.0.11');

COMMIT;

