using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Data
{
    /// <summary>
    /// Indexed lookup helpers for ArchivedEmails.
    ///
    /// MessageId is an unbounded text column, so a plain btree index on it is not
    /// safe: btree entries are limited to roughly 2.7 KB and the synthetic fallback
    /// ids built from From/To/Subject can exceed that, which would make archiving
    /// such emails fail. The duplicate checks therefore go through the expression
    /// index IX_ArchivedEmails_Account_MessageIdMd5 on
    /// ("MailAccountId", md5("MessageId")). The additional direct equality on
    /// "MessageId" guards against md5 collisions; it is evaluated only on the few
    /// rows the index returns.
    ///
    /// Without this, every duplicate check during a sync scanned all archived rows
    /// of the account, which saturated database I/O and caused frontend timeouts
    /// while syncs were running.
    /// </summary>
    public static class ArchivedEmailQueries
    {
        /// <summary>
        /// Returns the archived emails of an account with exactly the given
        /// MessageId, using the (MailAccountId, md5(MessageId)) expression index.
        /// Composable with FirstOrDefaultAsync/AnyAsync/Select like a normal query.
        /// </summary>
        public static IQueryable<ArchivedEmail> ByAccountAndMessageId(
            this MailArchiverDbContext context, int mailAccountId, string messageId)
        {
            return context.ArchivedEmails.FromSqlInterpolated($@"
                SELECT * FROM mail_archiver.""ArchivedEmails""
                WHERE ""MailAccountId"" = {mailAccountId}
                  AND md5(""MessageId"") = md5({messageId})
                  AND ""MessageId"" = {messageId}");
        }
    }
}
