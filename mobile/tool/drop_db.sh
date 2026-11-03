#!/usr/bin/env bash
# Drops a throwaway LocalDB database created by dev_api.sh.   usage: tool/drop_db.sh <database>
DB=${1:?database}
case "$DB" in ERP_Dev|ERP_Staging|ERP_Prod) echo "refusing to drop $DB"; exit 1;; esac
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -Q "IF DB_ID('$DB') IS NOT NULL BEGIN ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DB]; END"
