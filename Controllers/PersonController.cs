using API.Context;
using API.DTOs;
using API.Models;
using ExternalServiceCommunication.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ExternalCommunicationExample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PersonController(IPersonService personService, ApplicationDbContext dbContext) : ControllerBase
    {
        private readonly IPersonService _personService = personService;
        private readonly ApplicationDbContext _dbContext = dbContext;

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] AddPersonRequest request)
        {
            var externalResponse = await _personService.Get(request.Name);
            if (externalResponse.Success && externalResponse.Data is not null)
            {
                var newPerson = new Person()
                {
                    Count = externalResponse.Data.Count,
                    Name = request.Name,
                    LastName = request.LastName,
                    Gender = externalResponse.Data.Gender,
                    Probability = externalResponse.Data.Probability,
                };
                await _dbContext.AddAsync(newPerson);
                await _dbContext.SaveChangesAsync();
                return Ok(newPerson);
            }
            else
            {
                return BadRequest(externalResponse.Message);
            }

        }
    }
}
