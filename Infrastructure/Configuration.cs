using EmployeeReview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmployeeReview.Infrastructure.Data.Configurations
{
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Department)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.DateOfJoining)
                .IsRequired();

            builder.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Relationship with PerformanceReviews as an employee
            builder.HasMany(e => e.Reviews)
                .WithOne(r => r.Employee)
                .HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with PerformanceReviews as a reviewer
            builder.HasMany(e => e.ReviewsAsReviewer)
                .WithOne(r => r.Reviewer)
                .HasForeignKey(r => r.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }



    public class PerformanceReviewConfiguration : IEntityTypeConfiguration<PerformanceReview>
    {
        public void Configure(EntityTypeBuilder<PerformanceReview> builder)
        {
            builder.HasKey(r => r.Id);

            builder.Property(r => r.ReviewDate)
                .IsRequired();

            builder.Property(r => r.Score)
                .IsRequired()
                .HasColumnType("REAL"); // SQLite type for double with decimal places

            builder.Property(r => r.Comments)
                .HasMaxLength(1000);

            // Relationship with Employee
            builder.HasOne(r => r.Employee)
                .WithMany(e => e.Reviews)
                .HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Reviewer (which is also an Employee)
            builder.HasOne(r => r.Reviewer)
                .WithMany(e => e.ReviewsAsReviewer)
                .HasForeignKey(r => r.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}