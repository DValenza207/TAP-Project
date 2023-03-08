using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAP21_22_AuctionSite.Interface;
using System.Data.SqlClient;

namespace AuctionSite
{
    
    public class ASDbContext : TapDbContext
    {
        public ASDbContext(string dbConnectionString) : base(GetOptions(dbConnectionString)) 
        {
        }

        private static DbContextOptions GetOptions(string dbConnectionString)
        {
            return SqlServerDbContextOptionsExtensions.UseSqlServer(new DbContextOptionsBuilder(), dbConnectionString).Options;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //Relazione Sito-Utente 
            modelBuilder.Entity<DbUser>()
                .HasOne<DbSite>(s => s.DbSite)
                .WithMany(u => u.DbUsers)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            //Relazione Sito-Sessione
            modelBuilder.Entity<DbSession>()
                .HasOne<DbSite>(s => s.DbSite)
                .WithMany(u => u.DbSessions)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            //Relazione Sito-Asta
            modelBuilder.Entity<DbAuction>()
                .HasOne<DbSite>(s => s.DbSite)
                .WithMany(u => u.DbAuctions)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            //Chiave composta di Auction
            modelBuilder.Entity<DbAuction>()
                .HasKey(o => new {o.DbAuctionId, o.SiteName});
            //Chiave composta di Session
            modelBuilder.Entity<DbSession>()
                .HasKey(c => new {c.Id, c.SiteName});
            //Chiave composta di User
            modelBuilder.Entity<DbUser>()
                .HasKey(k => new {k.SiteName, k.Username});
        }

        public DbSet<DbSite> DbSites { get; set; }
        public DbSet<DbUser> DbUsers { get; set; }
        public DbSet<DbSession> DbSessions { get; set; }
        public DbSet<DbAuction> DbAuctions { get; set; }

        
    }

    [Table("Site")]
    [Index(nameof(Name),IsUnique = true)]
    public class DbSite
    {
        [Key]
        public virtual int DbSiteId { get; set; }
        [Required,
         MinLength(DomainConstraints.MinSiteName),
         MaxLength(DomainConstraints.MaxSiteName)]
        public virtual string Name { get; set; }
        public virtual int TimeZone { get; set; }
        public virtual int SessionExpirationTimeinSeconds { get; set; }
        public virtual double MinBidIncrement { get; set; }
        public virtual ICollection<DbUser> DbUsers { get; set; }
        public virtual ICollection<DbAuction> DbAuctions { get; set; }
        public virtual ICollection<DbSession> DbSessions { get; set; }

    }

    [Table("User")]
    public class DbUser
    {
        
        [Column(Order = 0)]
        [MinLength(DomainConstraints.MinSiteName)]
        [MaxLength(DomainConstraints.MaxSiteName)]
        public virtual string SiteName { get; set; }
        
        [Column(Order = 1)]
        [MinLength(DomainConstraints.MinUserName)]
        [MaxLength(DomainConstraints.MaxUserName)]
        public virtual string Username { get; set; }
        public virtual DbSite DbSite { get; set; }
        [ForeignKey("DbSite")]
        public virtual int DbSiteId { get; set; }
        [Required, MinLength(DomainConstraints.MinUserPassword)]
        public virtual string Password { get; set; }
    }

    [Table("Auction")]
    public class DbAuction
    {
        
        [Column(Order = 0)]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public virtual int DbAuctionId { get; set; }
        [Column(Order = 1)]
        
        [MinLength(DomainConstraints.MinSiteName)]
        [MaxLength(DomainConstraints.MaxSiteName)]
        public virtual string SiteName { get; set; }
        [Required]
        public virtual string Description { get; set; }
        public virtual DateTime EndsOn { get; set; }
        public virtual string Seller { get; set; }
        public virtual DbSite DbSite { get; set; }
        [ForeignKey("DbSite")]
        public virtual int DbSiteId { get; set; }
        public virtual double ActualPrice { get; set; }
        public virtual double MaximumOffer { get; set; }
        public virtual string Winner { get; set; }
    }

    [Table("Session")]
    public class DbSession
    {
        
        [Column(Order = 0)]
        public virtual string Id { get; set; }
        
        [Column(Order = 1)]
        [MinLength(DomainConstraints.MinSiteName)]
        [MaxLength(DomainConstraints.MaxSiteName)]
        public virtual string SiteName { get; set; }
        [Required]
        [MinLength(DomainConstraints.MinUserName)]
        [MaxLength(DomainConstraints.MaxUserName)]
        public virtual string Username { get; set; }
        public virtual DateTime ValidUntil { get; set; }
        public virtual int TimeZone { get; set; }
        [ForeignKey("DbSite")]
        public virtual int DbSiteId { get; set; }
        public virtual DbSite DbSite { get; set; }
    }
}
