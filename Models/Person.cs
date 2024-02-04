using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class Person
    {
        [Key]
        public int Id { get; set; }
        public int Count { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string LastName { get; set; }
        public string? Gender { get; set; }
        public double? Probability { get; set; }
    }
}
