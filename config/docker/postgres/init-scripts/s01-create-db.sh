#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
	CREATE DATABASE stopreasons;
	CREATE USER stopreasonsdbo WITH PASSWORD 'srdbo';
	ALTER DATABASE stopreasons OWNER TO stopreasonsdbo;
EOSQL