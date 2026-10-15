
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SoundBite.Data
{
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(80)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Bio { get; set; }
    }
}