using IsoTreatmentProcessSupportAPI.Converters;
using Microsoft.EntityFrameworkCore;

namespace IsoTreatmentProcessSupportAPI.Entities
{
    public class IsoSupportDbContext : DbContext
    {
        private string _connectionString = "Server=localhost;Database=IsoTreatmentProcessSupport;User Id=sa;Password=boxsy2-xobpyp-tonmUs;Encrypt=true;TrustServerCertificate=true;";
        public DbSet<User> Users { get; set; }
        public DbSet<Entry> Entries { get; set; }
        public DbSet<Reminder> Reminders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .Property(u => u.FirstName)
                .IsRequired()
                .HasMaxLength(50);

            modelBuilder.Entity<User>()
                .Property(u => u.LastName)
                .IsRequired()
                .HasMaxLength(60);

            modelBuilder.Entity<User>()
                .Property(u => u.Email)
                .IsRequired();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(_connectionString);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        {
            base.ConfigureConventions(builder);
            builder.Properties<TimeOnly>()
                .HaveConversion<TimeOnlyConverter>();
        }
    }
}
