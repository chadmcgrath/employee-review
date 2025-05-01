using EmployeeReview.Contracts.DTOs;
using EmployeeReview.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using EmployeeReview.Application.Services;

namespace EmployeeReview.Api.Controllers.REST
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly IPerformanceReviewService _reviewService;
        private readonly IEmployeeService _employeeService;
        private readonly ILogger<ReviewsController> _logger;

        public ReviewsController(
            IPerformanceReviewService reviewService,
            IEmployeeService employeeService,
            ILogger<ReviewsController> logger)
        {
            _reviewService = reviewService ?? throw new ArgumentNullException(nameof(reviewService));
            _employeeService = employeeService ?? throw new ArgumentNullException(nameof(employeeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: api/v1/reviews
        [HttpGet]
        [Authorize(Policy = PolicyNames.AdminOnly)]
        [ProducesResponseType(typeof(IEnumerable<PerformanceReviewDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReviews([FromQuery] string searchTerm = null, DateTime? fromDate = null)
        {
            _logger.LogInformation("Getting all performance reviews");

            var reviews = await _reviewService.GetReviewsAsync(searchTerm, fromDate);

            return Ok(reviews);
        }

        // POST: api/v1/reviews
        [HttpPost]
        [Authorize(Policy = PolicyNames.AdminOnly)]
        [ProducesResponseType(typeof(PerformanceReviewDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateReview([FromBody] CreatePerformanceReviewDto reviewDto)
        {
            _logger.LogInformation("Creating a new review for employee id={EmployeeId} by reviewer id={ReviewerId}",
                reviewDto.EmployeeId, reviewDto.ReviewerId);

            // Check if employee exists
            if (!await _employeeService.EmployeeExistsAsync(reviewDto.EmployeeId))
                return NotFound($"Employee with ID {reviewDto.EmployeeId} not found");

            // Check if reviewer exists
            if (!await _employeeService.EmployeeExistsAsync(reviewDto.ReviewerId))
                return NotFound($"Reviewer with ID {reviewDto.ReviewerId} not found");

            try
            {
                var createdReview = await _reviewService.CreateReviewAsync(reviewDto);
                return CreatedAtAction(nameof(GetReview), new { id = createdReview.Id }, createdReview);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid review data");
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Entity not found");
                return NotFound(ex.Message);
            }
        }

        // GET: api/v1/reviews/5
        [HttpGet("{id}")]
        [Authorize(Policy = PolicyNames.AdminOrReviewerOrEmployee)]
        [ProducesResponseType(typeof(PerformanceReviewDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetReview(int id)
        {
            _logger.LogInformation("Getting review with id={Id}", id);

            var review = await _reviewService.GetReviewByIdAsync(id);
            if (review == null)
                return NotFound();

            return Ok(review);
        }

        // GET: api/v1/employees/5/reviews
        [HttpGet("~/api/v{version:apiVersion}/employees/{employeeId}/reviews")]
        [Authorize(Policy = PolicyNames.AdminOrReviewerOrEmployee)]
        [ProducesResponseType(typeof(IEnumerable<PerformanceReviewDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEmployeeReviews(int employeeId)
        {
            _logger.LogInformation("Getting reviews for employee id={EmployeeId}", employeeId);

            // Check if employee exists
            if (!await _employeeService.EmployeeExistsAsync(employeeId))
                return NotFound($"Employee with ID {employeeId} not found");

            var reviews = await _reviewService.GetReviewsByEmployeeIdAsync(employeeId);
            return Ok(reviews);
        }
        // PUT: api/v1/reviews/5
        [HttpPut("{id}")]
        [Authorize(Policy = PolicyNames.AdminOnly)]
        [ProducesResponseType(typeof(PerformanceReviewDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateReview(int id, [FromBody] UpdatePerformanceReviewDto reviewDto)
        {
            _logger.LogInformation("Updating review with id={Id}", id);

            try
            {
                var updatedReview = await _reviewService.UpdateReviewAsync(id, reviewDto);
                if (updatedReview == null)
                    return NotFound($"Review with ID {id} not found");

                return Ok(updatedReview);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid review data");
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Entity not found");
                return NotFound(ex.Message);
            }
        }

        // DELETE: api/v1/reviews/5
        [HttpDelete("{id}")]
        [Authorize(Policy = PolicyNames.AdminOnly)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteReview(int id)
        {
            _logger.LogInformation("Deleting review with id={Id}", id);

            var result = await _reviewService.DeleteReviewAsync(id);
            if (!result)
                return NotFound($"Review with ID {id} not found");

            return NoContent();
        }
        // GET: api/v1/reviews/analytics
        [HttpGet("analytics")]
        [Authorize(Policy = PolicyNames.AdminOnly)]
        [ProducesResponseType(typeof(PerformanceAnalyticsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAnalytics()
        {
            _logger.LogInformation("Getting performance analytics");

            var analytics = await _reviewService.GetPerformanceAnalyticsAsync();
            return Ok(analytics);
        }
    }
}