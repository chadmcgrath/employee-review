
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;


namespace EmployeeReview.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class DesignDecisionsController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DesignDecisionsController> _logger;

        public DesignDecisionsController(
            IWebHostEnvironment environment,
            ILogger<DesignDecisionsController> logger)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: api/v1/designdecisions
        [HttpGet]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetDesignDecisionsDocument()
        {
            _logger.LogInformation("Retrieving design decisions document");

            var filePath = Path.Combine(_environment.ContentRootPath, "Documents", "ReadMe.docx");

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("Design decisions document not found at {FilePath}", filePath);
                return NotFound("Design decisions document not found");
            }

            _logger.LogInformation("Returning design decisions document from {FilePath}", filePath);
            return PhysicalFile(filePath, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "ReadMe.docx");
        }
    }
}