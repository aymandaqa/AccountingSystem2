
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.ExtendedProperties;
using Roadfn.Models;
using Roadfn.Services;

namespace AccountingSystem.Data
{
    public class RoadFnDbContext : DbContext
    {
        private string _user = "";
        //public CARGOContext( UserResolverService userService)
        //{
        //    _user = userService.GetUser();
        //}

        public RoadFnDbContext(DbContextOptions<RoadFnDbContext> options, UserResolverService userService)
            : base(options)
        {
            //ChangeTracker.Tracked += ChangeTracker_Tracked;
            ChangeTracker.StateChanged += ChangeTracker_StateChanged;
            //SavingChanges += UniversityContext_SavingChanges;
            //SavedChanges += UniversityContext_SavedChanges;
            //SaveChangesFailed += UniversityContext_SaveChangesFailed;
            _user = userService.GetUser();
        }



        private void ChangeTracker_Tracked(object sender, EntityTrackedEventArgs e)
        {
            Console.WriteLine($"Marked for Tracking: {e.Entry.Entity}");
        }
        private void ChangeTracker_StateChanged(object sender, EntityStateChangedEventArgs e)
        {
            // YOU CAN USE AN INTERFACE OR A BASE CLASS
            // But, for this demo, we are directly typecasting to Student model
            var student = e.Entry;
            switch (e.Entry.State)
            {
                case EntityState.Deleted:
                    //student.DeletedOn = DateTime.Now;
                    Console.WriteLine($"Marked for delete: {e.Entry.Entity}");
                    break;
                case EntityState.Modified:
                    //student.ModifiedOn = DateTime.Now;
                    var modifiedEntries = ChangeTracker.Entries().Where(e => e.State == EntityState.Modified);
                    foreach (EntityEntry entity in modifiedEntries)
                    {
                        string Id = "";
                        foreach (var propName in entity.CurrentValues.Properties)
                        {
                            if (propName.IsPrimaryKey())
                            {
                                Id = entity.CurrentValues[propName.Name].ToString();
                            }

                        }
                        try
                        {
                            foreach (var propName in entity.CurrentValues.Properties)
                            {
                                var oldValue = entity.OriginalValues[propName.Name];
                                var NewValue = entity.CurrentValues[propName.Name];
                                if (oldValue?.ToString() != NewValue?.ToString())
                                {
                                    EntitiesChanges entitiesChanges = new EntitiesChanges();
                                    entitiesChanges.PropName = propName.Name;
                                    entitiesChanges.NewValue = NewValue?.ToString();
                                    entitiesChanges.OldValue = oldValue?.ToString();
                                    entitiesChanges.EntityId = Id;
                                    entitiesChanges.IUser = _user;
                                    if (propName.DeclaringType is IEntityType entityType)
                                    {
                                        entitiesChanges.TableName = entityType.Name;
                                    }
                                    else
                                    {
                                        entitiesChanges.TableName = propName.DeclaringType?.Name ?? string.Empty;
                                    }
                                    this.EntitiesChanges.Add(entitiesChanges);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error tracking entity changes: {ex}");
                        }

                    }
                    Console.WriteLine($"Marked for update: {e.Entry.Entity}");
                    break;
                case EntityState.Added:
                    //student.CreatedOn = DateTime.Now;
                    Console.WriteLine($"Marked for insert: {e.Entry.Entity}");
                    break;
            }
        }
        private void UniversityContext_SavingChanges(object sender, SavingChangesEventArgs e)
        {
            Console.WriteLine($"Saving Changes at {DateTime.Now}");
        }

        private void UniversityContext_SavedChanges(object sender, SavedChangesEventArgs e)
        {
            Console.WriteLine($"Saved Chagnes at {DateTime.Now}");
        }

        private void UniversityContext_SaveChangesFailed(object sender, SaveChangesFailedEventArgs e)
        {
            Console.WriteLine($"Save Chagnes Failed at {DateTime.Now}");

        }
        public virtual DbSet<PayBusinessSlipView> PayBusinessSlipViews { get; set; }

        public virtual DbSet<ShipmentLogGrid2> ShipmentLogGrid2s { get; set; }

        public virtual DbSet<RPTPaymentHistoryUser> RPTPaymentHistoryUsers { get; set; }

        public virtual DbSet<RptpaymentHistoryDriver> RptpaymentHistoryDriver { get; set; }

        public virtual DbSet<BusinessRetStatementBulk> BusinessRetStatementBulk { get; set; }
        public virtual DbSet<Area> Areas { get; set; }

        public virtual DbSet<ShipmentsType> ShipmentsTypes { get; set; }

        public virtual DbSet<Drife> Drives { get; set; }
        public virtual DbSet<ShipmentStatus> ShipmentStatuses { get; set; }

        public virtual DbSet<ShipmentSummary> ShipmentSummary { get; set; }
        public virtual DbSet<InvoiceRetBusinessUserShipments> InvoiceRetBusinessUserShipments { get; set; }

        public virtual DbSet<EntitiesChanges> EntitiesChanges { get; set; }
        public virtual DbSet<MBusinessStatementBulk> MBusinessStatementBulk { get; set; }

        public virtual DbSet<InvoiceBusinessUserShipments> InvoiceBusinessUserShipments { get; set; }

        public virtual DbSet<BusinessStatementBulk> BusinessStatementBulk { get; set; }
        public virtual DbSet<ShipmentLink> ShipmentLink { get; set; }
        public virtual DbSet<ShipmentColorStatus> ShipmentColorStatus { get; set; }
        public virtual DbSet<BisnessUserReturnHeader> BisnessUserReturnHeader { get; set; }
        public virtual DbSet<BisnessUserReturnDetail> BisnessUserReturnDetail { get; set; }
        public virtual DbSet<DriverPay> DriverPay { get; set; }
        public virtual DbSet<BussPaymentsHist> BussPaymentsHist { get; set; }
        public virtual DbSet<BussRetPaymentsHist> BussRetPaymentsHist { get; set; }
        public virtual DbSet<BusinessReturnedBulk> BusinessReturnedBulk { get; set; }
        public virtual DbSet<InvoiceStatus> InvoiceStatus { get; set; }
        public virtual DbSet<RptDriverPaySlip> RptDriverPaySlip { get; set; }
        public virtual DbSet<BisnessUserPaymentDetail> BisnessUserPaymentDetails { get; set; }
        public virtual DbSet<BisnessUserPaymentHeader> BisnessUserPaymentHeaders { get; set; }
        public virtual DbSet<Roadfn.Models.City> Cities { get; set; }
        public virtual DbSet<CompanyBranch> CompanyBranches { get; set; }
        public virtual DbSet<DriverPaymentDetail> DriverPaymentDetails { get; set; }
        public virtual DbSet<DriverPaymentHeader> DriverPaymentHeader { get; set; }
        public virtual DbSet<RptDriverPay> RptDriverPay { get; set; }
        public virtual DbSet<SessionAddRemark> Session_Add_Remarks { get; set; }
        public virtual DbSet<Shipment> Shipments { get; set; }
        public virtual DbSet<ShipmentLog> ShipmentLogs { get; set; }
        public virtual DbSet<Roadfn.Models.User> Users { get; set; }

    }
}

