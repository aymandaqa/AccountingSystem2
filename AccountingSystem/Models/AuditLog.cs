using System;

namespace AccountingSystem.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public string TableName { get; set; }
        public string? RecordId { get; set; }
        public string Operation { get; set; }
        public string? ColumnName { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
    }
}
