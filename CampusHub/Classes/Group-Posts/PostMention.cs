using CampusHub.Classes.UserAccount;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CampusHub.Classes.Group_Posts
{
    [Table("PostMentions")]
    public class PostMention
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PostId { get; set; }

        [Required]
        public string MentionedUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Post Post { get; set; }

        public virtual AppUser MentionedUser { get; set; }
    }
}
