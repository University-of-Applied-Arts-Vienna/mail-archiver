using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <summary>
    /// Adds the expression index used by all per-message duplicate checks
    /// (sync, retention deletion, EML/MBox import). The index is on
    /// ("MailAccountId", md5("MessageId")) because MessageId is unbounded text:
    /// indexing the md5 keeps every entry within the btree size limit while still
    /// allowing exact lookups (queries compare md5("MessageId") = md5($1) plus a
    /// direct equality recheck, see Data/ArchivedEmailQueries.cs).
    ///
    /// The index is built CONCURRENTLY (outside the migration transaction) so a
    /// large existing archive stays readable and writable while it builds.
    /// </summary>
    public partial class MigrateV2606_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A previously interrupted CONCURRENTLY build leaves an INVALID index
            // behind, which CREATE INDEX IF NOT EXISTS would wrongly treat as
            // existing. Drop such a leftover first (cheap, transactional).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM pg_index i
                        JOIN pg_class c ON c.oid = i.indexrelid
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE n.nspname = 'mail_archiver'
                          AND c.relname = 'IX_ArchivedEmails_Account_MessageIdMd5'
                          AND NOT i.indisvalid
                    ) THEN
                        EXECUTE 'DROP INDEX mail_archiver.""IX_ArchivedEmails_Account_MessageIdMd5""';
                    END IF;
                END $$;
            ");

            // CONCURRENTLY must run outside a transaction block.
            migrationBuilder.Sql(@"
                CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_ArchivedEmails_Account_MessageIdMd5""
                ON mail_archiver.""ArchivedEmails"" (""MailAccountId"", md5(""MessageId""));
            ", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS mail_archiver.""IX_ArchivedEmails_Account_MessageIdMd5"";
            ");
        }
    }
}
