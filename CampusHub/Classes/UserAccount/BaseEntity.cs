using System.ComponentModel.DataAnnotations;

namespace CampusHub.Classes.UserAccount
{
    public class BaseEntity
    {
        public BaseEntity()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }


        [Key]
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public void UpdateTimeStamp()
        {
            UpdatedAt = DateTime.UtcNow;
        }

        public static DateTime ConvertToLocalTime(DateTime utcTime, string timeZoneId)
        {
            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, timeZoneInfo);
        }
    }
}
