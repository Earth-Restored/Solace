using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Solace.DB.Utils;

public static class ExceptionExtensions
{
    extension(DbUpdateException exception)
    {
        public bool IsUniqueConstraintViolation
        {
            get
            {
                if (exception.InnerException is SqliteException sqliteEx)
                {
                    if (sqliteEx is { SqliteErrorCode: 19, SqliteExtendedErrorCode: 2067 or 2579 })
                    {
                        return true;
                    }

                    if (sqliteEx.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}