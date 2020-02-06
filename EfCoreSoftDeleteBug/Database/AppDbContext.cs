using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EfCoreSoftDeleteBug.Database
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }

        public DbSet<Person> Person { get; set; }

        public DbSet<Country> Country { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("uuid-ossp");

            modelBuilder.Entity<Person>().HasQueryFilter(x => x.DeletedAt == null);
            modelBuilder.Entity<Country>().HasQueryFilter(x => x.DeletedAt == null);

            base.OnModelCreating(modelBuilder);
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ChangeTracker.DetectChanges();

            var markedAsDeleted = ChangeTracker.Entries().Where(x => x.State == EntityState.Deleted).ToList();

            foreach (var item in markedAsDeleted)
            {
                if (item.Entity is BaseModel entity)
                {
                    // Set the entity to unchanged (if we mark the whole entity as Modified, every field gets sent to Db as an update)
                    item.State = EntityState.Unchanged;

                    entity.DeletedAt = DateTime.Now;
                }
            }

            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            foreach (var item in markedAsDeleted)
            {
                item.State = EntityState.Deleted;
            }

            ChangeTracker.AcceptAllChanges();

            return result;
        }
    }

    public class Person : BaseModel
    {
        public string Name { get; set; }

        public Guid CountryId { get; set; }

        public virtual Country Country { get; set; }
    }

    public class Country : BaseModel
    {
        public string Name { get; set; }

        public virtual ICollection<Person> People { get; set; }
    }

    public class BaseModel
    {
        [Key]
        public Guid Id { get; set; }

        public DateTime? DeletedAt { get; set; }
    }
}
