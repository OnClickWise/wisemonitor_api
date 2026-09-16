using System.Collections.Generic;

namespace WiseMonitor.Api.DTOs
{
    public class ProductivityClassificationSaveDTO
    {
        public List<ProductivityClassificationItemDTO> Items { get; set; }
            = new();
    }
}
