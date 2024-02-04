using API.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Context
{
    public class ApplicationDbContext : DbContext
    {

        public ApplicationDbContext(DbContextOptions options) : base(options)
        {

        }
        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    base.OnModelCreating(modelBuilder);
        //    modelBuilder.Entity<Person>();
        //}

        public DbSet<Person> Persons { get; set; }

    }
}
