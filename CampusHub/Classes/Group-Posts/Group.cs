using CampusHub.Enums;
using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusHub.Classes.Group_Posts
{
    public class Group
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public GroupPrivacy Privacy { get; set; } // New field
        [NotMapped]
        public string SearchVector { get; set; }

        public ICollection<GroupMemberPage> Members { get; set; } = new List<GroupMemberPage>();
        public ICollection<Post> Posts { get; set; } = new List<Post>();
    }
}
