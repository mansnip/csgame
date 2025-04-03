using Microsoft.EntityFrameworkCore;

namespace csgame.Context
{
    public class csGameDbContext : DbContext
    {
        public csGameDbContext(DbContextOptions<csGameDbContext> options) : base(options)
        {
        }


    }
}
