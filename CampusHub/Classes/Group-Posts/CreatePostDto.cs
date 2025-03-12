namespace CampusHub.Classes.Group_Posts
{
    public class CreatePostDto
    {
        public string GroupId { get; set; }
        public string Description { get; set; }
        public IFormFile? File { get; set; }
    }
}
