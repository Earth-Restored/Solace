using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Solace.DB.Earth.Utils;

public static class ExceptionExtensions
{
    extension(DbUpdateException exception)
    {
        public bool IsUniqueConstraintViolation
        {
            get
            {
                if (exception.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
                {
                    return true;
                }

                return false;
            }
        }
    }
}