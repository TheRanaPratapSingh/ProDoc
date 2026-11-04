# Database backup and restore

## Goals

- Daily full backups of the ProDoc SQL database
- Point-in-time recovery where the hosting tier supports it
- Verified restore to a **non-production** instance before go-live

## Azure SQL (recommended)

1. Enable automated backups (default on Azure SQL).
2. Configure long-term retention if required by policy (e.g. 30–90 days).
3. Restore drill quarterly:

```powershell
# Portal: Azure SQL → Restore → new database name ProDoc_RestoreDrill_YYYYMMDD
# Or use az sql db restore ...
```

4. Point the staging API at the restored DB (read-only smoke) and run:

```powershell
dotnet test tests/ProDoc.Api.Tests --filter "FullyQualifiedName~AuthFlowTests"
curl https://staging-api/health/ready
```

5. Drop the restore-drill database after verification.

## SQL Server Express / VM

```powershell
# Backup
sqlcmd -S .\SQLEXPRESS -E -Q "BACKUP DATABASE [ProDoc] TO DISK='D:\Backups\ProDoc_full.bak' WITH INIT, COMPRESSION"

# Restore (overwrite a dedicated drill database only)
sqlcmd -S .\SQLEXPRESS -E -Q "RESTORE DATABASE [ProDoc_Drill] FROM DISK='D:\Backups\ProDoc_full.bak' WITH MOVE 'ProDoc' TO 'D:\Data\ProDoc_Drill.mdf', MOVE 'ProDoc_log' TO 'D:\Data\ProDoc_Drill_log.ldf', REPLACE"
```

Script helper: `deploy/scripts/backup-sql.ps1`.

## Document blobs

If using local disk (`LocalDocumentStorage`), sync the storage root with the DB backup timestamp. Prefer Azure Blob with soft-delete before multi-instance production.

## Monitoring backups

- Alert when the last successful backup age exceeds 36 hours
- Alert on failed restore drills
- Never store BAK files in public storage containers

## Verification checklist (required before production)

- [ ] Full backup completed successfully
- [ ] Restore to isolated database succeeded
- [ ] API can authenticate against restored DB (staging credentials)
- [ ] Sample appointment/payment rows readable
- [ ] RPO/RTO agreed with operations
