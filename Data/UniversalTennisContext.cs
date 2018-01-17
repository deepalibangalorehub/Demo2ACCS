using UniversalTennis.Algorithm.Models;
using Microsoft.EntityFrameworkCore;

namespace UniversalTennis.Algorithm.Data
{
    public class UniversalTennisContext : DbContext
    {
        public UniversalTennisContext(DbContextOptions<UniversalTennisContext> options) : base(options)
        {
        }

        public DbSet<PlayerRating> PlayerRatings { get; set; }
        public DbSet<SubRating> SubRatings { get; set; }
        public DbSet<RatingResult> ResultRatings { get; set; }
        public DbSet<AlgorithmSettings> AlgorithmSettings { get; set; }
        public DbSet<RatingJob> RatingJobs { get; set; }
        public DbSet<PlayerEvent> PlayerEvents { get; set; }
        public DbSet<ResultEvent> ResultEvents { get; set; }
        public DbSet<WeeklyAverage> WeeklyAverages { get; set; }
        public DbSet<DailyRating> DailyRatings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlayerRating>().ToTable("PlayerRating");
            modelBuilder.Entity<SubRating>().ToTable("SubRating");
            modelBuilder.Entity<RatingResult>().ToTable("RatingResult");
            modelBuilder.Entity<AlgorithmSettings>().ToTable("AlgorithmSetting");
            modelBuilder.Entity<RatingJob>().ToTable("RatingJob");
            modelBuilder.Entity<PlayerEvent>().ToTable("PlayerEvent");
            modelBuilder.Entity<ResultEvent>().ToTable("ResultEvent");
            modelBuilder.Entity<WeeklyAverage>().ToTable("WeeklyAverage");
            modelBuilder.Entity<DailyRating>().ToTable("DailyRating");
            modelBuilder.Entity<DailyRating>(entity =>
            {
                entity.HasOne(p => p.PlayerRating)
                    .WithMany(p => p.DailyRatings)
                    .HasForeignKey(d => d.PlayerRatingId)
                    .HasConstraintName("FK_DailyRating_PlayerRating")
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<WeeklyAverage>(entity =>
            {
                entity.HasOne(p => p.PlayerRating)
                    .WithMany(p => p.WeeklyAverages)
                    .HasForeignKey(d => d.PlayerRatingId)
                    .HasConstraintName("FK_WeeklyAverage_PlayerRating")
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
