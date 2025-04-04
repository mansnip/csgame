using csgame.Models;
using Microsoft.EntityFrameworkCore;

namespace csgame.Context
{
    public class csGameDbContext : DbContext
    {
        public csGameDbContext(DbContextOptions<csGameDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // تنظیمات مدل فاکتور
            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.InvoiceNumber)
                .IsUnique();

            modelBuilder.Entity<Invoice>()
                .Property(i => i.BasePrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Invoice>()
                .Property(i => i.DiscountAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Invoice>()
                .Property(i => i.FinalPrice)
                .HasColumnType("decimal(18,2)");

            // رابطه User و Subscription
            modelBuilder.Entity<Subscription>()
                .HasOne(s => s.User)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // رابطه User و Invoice
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.User)
                .WithMany(u => u.Invoices)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // رابطه Invoice و Subscription (یک به یک)
            modelBuilder.Entity<Subscription>()
                .HasOne(s => s.Invoice)
                .WithOne(i => i.Subscription)
                .HasForeignKey<Subscription>(s => s.InvoiceId)
                .OnDelete(DeleteBehavior.NoAction);

        }
    }
}
