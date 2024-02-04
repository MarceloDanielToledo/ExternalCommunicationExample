using System.ComponentModel.DataAnnotations;

namespace API.DTOs
{
    public class AddPersonRequest
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string LastName { get; set; }
    }
}
