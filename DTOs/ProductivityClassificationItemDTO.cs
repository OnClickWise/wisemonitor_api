using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.DTOs
{
    public class ProductivityClassificationItemDTO
    {
        public string Identifier { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public ProductivityItemType ItemType { get; set; }

        public ActivityCategory Category { get; set; }
    }
}
