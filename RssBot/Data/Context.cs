using Microsoft.EntityFrameworkCore;

namespace RssBot.Data
{
    class Context : DbContext
    {
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<RssFeed> Feeds { get; set; }

        public Context(DbContextOptions<Context> options) : base(options)
        {
            Database.EnsureCreated();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Subscription>().HasKey(s => new { s.UserId, s.ShortId });
            modelBuilder.Entity<RssFeed>().HasKey(r => r.Url);
        }
    }
}
