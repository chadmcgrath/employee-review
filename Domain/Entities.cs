
namespace EmployeeReview.Domain.Entities
{
    public class Employee
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public string Email { get; private set; }
        public string Department { get; private set; }
        public DateTime DateOfJoining { get; private set; }
        public bool IsActive { get; private set; }

        // Navigation properties
        public virtual ICollection<PerformanceReview> Reviews { get; private set; }
        public virtual ICollection<PerformanceReview> ReviewsAsReviewer { get; private set; }
        

        // For EF Core
        protected Employee() { }
    

        public Employee(string name, string email, string department, DateTime dateOfJoining)
        {
            ValidateEmployee(name, email, department, dateOfJoining);

            Name = name;
            Email = email;
            Department = department;
            DateOfJoining = dateOfJoining;
            IsActive = true;
            Reviews = new List<PerformanceReview>();
            ReviewsAsReviewer = new List<PerformanceReview>();
        }

        public void Update(string name, string email, string department)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty", nameof(name));

            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty", nameof(email));

            if (string.IsNullOrWhiteSpace(department))
                throw new ArgumentException("Department cannot be empty", nameof(department));

            Name = name;
            Email = email;
            Department = department;
        }

        public void SoftDelete()
        {
            IsActive = false;
        }

        public void Restore()
        {
            IsActive = true;
        }

        private void ValidateEmployee(string name, string email, string department, DateTime dateOfJoining)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty", nameof(name));

            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty", nameof(email));

            if (string.IsNullOrWhiteSpace(department))
                throw new ArgumentException("Department cannot be empty", nameof(department));

            if (dateOfJoining > DateTime.Now)
                throw new ArgumentException("Date of joining cannot be in the future", nameof(dateOfJoining));
        }
    }


    public class PerformanceReview
    {
        public int Id { get; private set; }
        public int EmployeeId { get; private set; }
        public int ReviewerId { get; private set; }
        public DateTime ReviewDate { get; private set; }
        public double Score { get; private set; }
        public string Comments { get; private set; }

        // Navigation properties
        public virtual Employee Employee { get; private set; }
        public virtual Employee Reviewer { get; private set; }

        // For EF Core
        protected PerformanceReview() { }

        public PerformanceReview(int employeeId, int reviewerId, DateTime reviewDate, double score, string comments)
        {
            ValidateReview(score, reviewDate);

            EmployeeId = employeeId;
            ReviewerId = reviewerId;
            ReviewDate = reviewDate;
            Score = Math.Round(score, 1); // Limit to 1 decimal place
            Comments = comments;
        }

        public void Update(DateTime reviewDate, double score, string comments)
        {
            ValidateReview(score, reviewDate);

            ReviewDate = reviewDate;
            Score = Math.Round(score, 1); // Limit to 1 decimal place
            Comments = comments;
        }

        private void ValidateReview(double score, DateTime reviewDate)
        {
            if (score < 1.0 || score > 5.0)
                throw new ArgumentException("Score must be between a value from 1.0 to 5.0", nameof(score));

            if (reviewDate > DateTime.Now)
                throw new ArgumentException("Review date cannot be in the future", nameof(reviewDate));
        }
    }
}