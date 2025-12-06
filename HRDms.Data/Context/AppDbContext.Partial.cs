using HRDms.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace HRDms.Data.Context;

public partial class AppDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // PerformanceReview – Employee ilişkilerini burada NET tanımlıyoruz

        modelBuilder.Entity<PerformanceReview>(entity =>
        {
            // ÇALIŞANIN KENDİ PERFORMANS KAYDI
            entity.HasOne(d => d.Employee)
                .WithMany(p => p.PerformanceReviewEmployees)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction) // DB zaten hallediyor
                .HasConstraintName("FK_PerformanceReviews_Employee");

            // DEĞERLENDİRİCİ (REVIEWER)
            entity.HasOne(d => d.Reviewer)
                .WithMany(p => p.PerformanceReviewReviewers)
                .HasForeignKey(d => d.ReviewerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PerformanceReviews_Reviewer");
        });
    }
}
