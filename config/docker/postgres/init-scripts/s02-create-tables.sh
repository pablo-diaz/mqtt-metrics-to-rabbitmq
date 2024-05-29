#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username stopreasonsdbo --dbname stopreasons <<-EOSQL
    CREATE TABLE device_downtime_reason (
        id bigint PRIMARY KEY,
        device_id VARCHAR(100) NOT NULL,
        initially_stopped_at TIMESTAMP WITH TIME ZONE NOT NULL,
        last_stopped_metric_traced_at TIMESTAMP WITH TIME ZONE NOT NULL,
        maybe_stopping_reason VARCHAR(10) DEFAULT NULL
    );
EOSQL

