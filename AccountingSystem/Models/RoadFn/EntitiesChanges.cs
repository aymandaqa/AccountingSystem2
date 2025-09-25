namespace Roadfn.Models
{
    public class EntitiesChanges
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? TableName { get; set; }
        public string? EntityId { get; set; }
        public string? PropName { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime Idate { get; set; } = DateTime.Now;
        public string? IUser { get; set; }
    }
}
