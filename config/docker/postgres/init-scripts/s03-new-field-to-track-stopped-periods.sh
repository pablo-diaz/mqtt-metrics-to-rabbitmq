#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username stopreasonsdbo --dbname stopreasons <<-EOSQL
    ALTER TABLE device_downtime_reason ADD is_it_still_stopped BOOLEAN DEFAULT FALSE;
EOSQL

