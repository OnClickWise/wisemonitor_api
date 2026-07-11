using System;

namespace WiseMonitor.Api.Models
{
    public class DepartmentMember
    {
        public Guid DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        // Director, Manager, Member
        public string MemberRole { get; set; } = "Member";

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
