namespace Roadfn.Models
{
    public class InvoiceBusinessUserShipments
    {
        public long ID { get; set; }
        public int UserID { get; set; }
        public decimal PaymentValue { get; set; }
        public DateTime PaymentDate { get; set; }
        public int LoginUserID { get; set; }
        public int? StatusId { get; set; }
        public string StatusName { get; set; }
        public string BusinessUserName { get; set; }
        public string EmpName { get; set; }
        public int? EmpNameBranchID { get; set; }
        public string BranchName { get; set; }

        public int? DriverId { get; set; }
        public string Driver { get; set; }

    }
    public class InvoiceRetBusinessUserShipments
    {
        public long ID { get; set; }
        public int UserID { get; set; }
        public decimal PaymentValue { get; set; }
        public DateTime PaymentDate { get; set; }
        public int LoginUserID { get; set; }
        public int? StatusId { get; set; }
        public string StatusName { get; set; }
        public string BusinessUserName { get; set; }
        public string EmpName { get; set; }
        public int? EmpNameBranchID { get; set; }
        public string BranchName { get; set; }

        public int? DriverId { get; set; }
        public string Driver { get; set; }

    }
}
