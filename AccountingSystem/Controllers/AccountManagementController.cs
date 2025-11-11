using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Roadfn.Models;
using Roadfn.ViewModel;
using Syncfusion.EJ2.Base;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Text;
using User = AccountingSystem.Models.User;

namespace Roadfn.Controllers
{
    [Authorize]
    public class AccountManagementController : Controller
    {
        private RoadFnDbContext _context;
        private ApplicationDbContext _accontext;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration iConfig;
        private readonly IJournalEntryService _journalEntryService;
        private readonly IAccountService _accountService;
        private readonly UserManager<User> _userManager;
        public AccountManagementController(RoadFnDbContext context, UserManager<User> userManager, IWebHostEnvironment env, IConfiguration iConfig, IJournalEntryService journalEntryService, IAccountService accountService, ApplicationDbContext accontext)
        {
            _context = context;
            _env = env;
            this.iConfig = iConfig;
            _journalEntryService = journalEntryService;
            _accountService = accountService;
            _accontext = accontext;
            _userManager = userManager;

        }

        [Authorize(Policy = "accountmanagement.driverstatment")]
        public async Task<IActionResult> DriverStatment()
        {

            var user = await _accontext.Users.FirstOrDefaultAsync(t => t.UserName == User.Identity.Name);

            var br = user.DriverAccountBranchIds.Split(",");
            ViewBag.CompanyBranches = await _context.CompanyBranches.Where(t => br.Contains(t.Id.ToString())).ToListAsync();

            return View();
        }

        public async Task<IActionResult> InvoiceBusinessUserShipments()
        {
            ViewBag.CompanyBranches = await _context.CompanyBranches.ToListAsync();
            ViewBag.InvoiceStatus = await _context.InvoiceStatus.ToListAsync();

            return View();
        }

        [Authorize(Policy = "accountmanagement.busnissstatment")]
        public async Task<IActionResult> BusnissStatment()
        {
            ViewBag.Driver = from t1 in _context.Users
                             where t1.UserType == "4"
                             select new { t1.Id, Name = $"{t1.UserName}-{t1.FirstName} {t1.LastName}" };
            var t = from c in _context.Users
                    where c.UserType == "3"
                    select new
                    {
                        Id = c.Id,
                        Name = c.FirstName + " " + c.LastName
                    };
            ViewBag.User = await t.ToListAsync();
            ViewBag.InvoiceStatus = await _context.InvoiceStatus.ToListAsync();

            var user = await _accontext.Users.FirstOrDefaultAsync(t => t.UserName == User.Identity.Name);

            var br = user.BusinessAccountBranchIds.Split(",");
            ViewBag.CompanyBranches = await _context.CompanyBranches.Where(t => br.Contains(t.Id.ToString())).ToListAsync();


            return View();
        }
        [Authorize(Policy = "accountmanagement.busnissshipmentsreturn")]
        public async Task<IActionResult> BusnissShipmentsReturn()
        {
            ViewBag.Driver = from t1 in _context.Users
                             where t1.UserType == "4"
                             select new { t1.Id, Name = $"{t1.UserName}-{t1.FirstName} {t1.LastName}" };
            var t = from c in _context.Users
                    where c.UserType == "3"
                    select new
                    {
                        Id = c.Id,
                        Name = c.FirstName + " " + c.LastName
                    };
            ViewBag.User = await t.ToListAsync();
            ViewBag.InvoiceStatus = await _context.InvoiceStatus.ToListAsync();
            ViewBag.CompanyBranches = await _context.CompanyBranches.ToListAsync();
            return View();
        }
        public async Task<IActionResult> GetBusnissUser([FromBody] Data dm, int branchId)
        {
            var t = from c in _context.BusinessStatementBulk
                    where c.CompanyBranchID == branchId
                    select new
                    {
                        Id = c.SenderId,
                        Name = c.SenderName
                    };
            var Data = await t.ToListAsync();
            if (dm.where != null)
            {
                Data = (from cust in Data
                        where cust.Name.ToLower().Contains(dm.@where[0].value.ToString())
                        select cust).ToList();
            }
            if (dm.take != 0)
                Data = Data.Take(dm.take).ToList();
            return Json(Data);
        }
        public async Task<IActionResult> GetMBusnissUser([FromBody] Data dm, int user)
        {
            var t = from c in _context.MBusinessStatementBulk
                    where c.SenderId == user
                    select new
                    {
                        Id = c.UserID,
                        Name = c.IUser
                    };
            var Data = await t.ToListAsync();
            if (dm.where != null)
            {
                Data = (from cust in Data
                        where cust.Name.ToLower().Contains(dm.@where[0].value.ToString())
                        select cust).ToList();
            }
            if (dm.take != 0)
                Data = Data.Take(dm.take).ToList();
            return Json(Data);
        }
        public async Task<IActionResult> GetBusinessReturnedBulk([FromBody] Data dm, int branchId)
        {
            var t = from c in _context.BusinessReturnedBulk
                    where c.CompanyBranchID == branchId
                    select new
                    {
                        Id = c.SenderId,
                        Name = c.SenderName
                    };
            var Data = await t.ToListAsync();
            if (dm.where != null)
            {
                Data = (from cust in Data
                        where cust.Name.ToLower().Contains(dm.@where[0].value.ToString())
                        select cust).ToList();
            }
            if (dm.take != 0)
                Data = Data.Take(dm.take).ToList();
            return Json(Data);
        }
        public async Task<IActionResult> GetDrivers([FromBody] Data dm, int Id = 0)
        {
            var t = from c in _context.DriverPay
                    where c.BranchID == Id
                    select new
                    {
                        Id = c.DriverID,
                        Name = c.DriverName
                    };
            var Data = await t.ToListAsync();
            if (dm.where != null)
            {
                Data = (from cust in Data
                        where cust.Name.ToLower().Contains(dm.@where[0].value.ToString())
                        select cust).ToList();
            }
            if (dm.take != 0)
                Data = Data.Take(dm.take).ToList();
            return Json(Data);
        }

        [Authorize(Policy = "accountmanagement.receivepayments")]
        public IActionResult ReceivePayments()
        {
            ViewBag.Driver = from t1 in _context.Users
                             where t1.UserType == "4"
                             select new { t1.Id, Name = $"{t1.UserName}-{t1.FirstName} {t1.LastName}" };
            //ViewBag.city = await _context.Cities.ToListAsync();

            return View();
        }
        [Authorize(Policy = "accountmanagement.receiveretpayments")]
        public async Task<IActionResult> ReceiveRetPayments()
        {
            ViewBag.Driver = from t1 in _context.Users
                             where t1.UserType == "4"
                             select new { t1.Id, Name = $"{t1.UserName}-{t1.FirstName} {t1.LastName}" };
            //ViewBag.city = await _context.Cities.ToListAsync();
            ViewBag.CompanyBranches = await _context.CompanyBranches.ToListAsync();

            return View();
        }
        [HttpPost]
        public async Task<IActionResult> ReceivingPayments([FromBody] List<InvoiceBusinessUserShipments> invoiceBusinessUserShipments)
        {
            string userId = User.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;
            var user = await _context.Users.Where(t => t.Id == Convert.ToInt32(userId)).FirstOrDefaultAsync();
            var msg = "";
            foreach (var item1 in invoiceBusinessUserShipments)
            {
                var inv = await _context.BisnessUserPaymentHeader.Where(t => t.Id == item1.ID).FirstOrDefaultAsync();
                if (inv != null)
                {
                    inv.StatusId = 2;
                    _context.BisnessUserPaymentHeader.Update(inv);
                    await _context.SaveChangesAsync();
                    msg += Environment.NewLine + $"تم تسليم الفاتورة {inv.Id} ";

                    BussPaymentsHist bussPaymentsHist = new BussPaymentsHist();
                    bussPaymentsHist.StatusId = 2;
                    bussPaymentsHist.DriverId = Convert.ToInt32(inv.DriverId);
                    bussPaymentsHist.BisnessUserPaymentHeader = inv.Id;
                    bussPaymentsHist.Iuser = Convert.ToInt32(userId);
                    await _context.BussPaymentsHist.AddAsync(bussPaymentsHist);
                    await _context.SaveChangesAsync();

                    var details = await _context.BisnessUserPaymentDetails.Where(t => t.HeaderId == inv.Id).ToListAsync();

                    var statusinv = await _context.InvoiceStatus.FindAsync(2);
                    List<Shipment> listpay = new List<Shipment>();
                    foreach (var item in details)
                    {
                        listpay.Add(await _context.Shipments.FindAsync(Convert.ToInt32(item.ShipmentId)));
                    }
                    foreach (var item in listpay)
                    {
                        var ship = await _context.Shipments.FindAsync(Convert.ToInt32(item.Id));
                        if (ship != null)
                        {
                            var newstatus = 0;
                            if (ship.RetStatus == true || ship.ShipmentTrackingNo.Contains("_ex"))
                            {
                                newstatus = 23;
                            }
                            else
                            {
                                newstatus = statusinv.TransferShipmentStatusTo;
                            }

                            //add Submitted status
                            ShipmentLog shipmentLog = new ShipmentLog();
                            shipmentLog.ShipmentId = ship.Id;
                            shipmentLog.EntryDate = DateTime.Now;
                            shipmentLog.EntryDateTine = DateTime.Now;
                            shipmentLog.UserId = Convert.ToInt32(userId);
                            shipmentLog.Status = newstatus;
                            shipmentLog.ClientName = ship.ClientName;
                            shipmentLog.ClientPhone = ship.ClientPhone;
                            shipmentLog.FromCityId = ship.FromCityId;
                            shipmentLog.ClientCityId = ship.ClientCityId;
                            shipmentLog.ClientAreaId = ship.ClientAreaId;
                            shipmentLog.IsUserBusiness = ship.IsUserBusiness;
                            shipmentLog.SenderName = ship.SenderName;
                            shipmentLog.SenderTel = ship.SenderTel;
                            shipmentLog.BusinessUserId = ship.BusinessUserId;
                            await _context.ShipmentLogs.AddAsync(shipmentLog);
                            await _context.SaveChangesAsync();

                            SessionAddRemark sessionAddRemark = new SessionAddRemark();
                            sessionAddRemark.ShipmentId = ship.Id;
                            sessionAddRemark.UserId = Convert.ToInt32(userId);
                            sessionAddRemark.EntryDateTime = DateTime.Now;
                            sessionAddRemark.OldStatus = ship.Status;
                            sessionAddRemark.NewStatus = newstatus;
                            sessionAddRemark.EntryDate = DateTime.Now;
                            ship.LastUpdate = DateTime.Now;
                            ship.Status = newstatus;
                            ship.DriverId = 0;
                            _context.Shipments.Update(ship);
                            await _context.SaveChangesAsync();
                            await _context.Session_Add_Remarks.AddAsync(sessionAddRemark);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                else
                {
                    msg += Environment.NewLine + $"لم يتم العثور على الفاتورة {inv.Id} ";

                }
            }

            return Json(msg);
        }

        [HttpPost]
        public async Task<IActionResult> ReceivingRetPayments([FromBody] List<InvoiceBusinessUserShipments> invoiceBusinessUserShipments)
        {
            string userId = User.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;
            var user = await _context.Users.Where(t => t.Id == Convert.ToInt32(userId)).FirstOrDefaultAsync();
            var msg = "";
            foreach (var item1 in invoiceBusinessUserShipments)
            {
                var inv = await _context.BisnessUserReturnHeader.Where(t => t.Id == item1.ID).FirstOrDefaultAsync();
                if (inv != null)
                {
                    inv.StatusId = 4;
                    _context.BisnessUserReturnHeader.Update(inv);
                    await _context.SaveChangesAsync();
                    msg += Environment.NewLine + $"تم تسليم الفاتورة {inv.Id} ";

                    BussRetPaymentsHist bussPaymentsHist = new BussRetPaymentsHist();
                    bussPaymentsHist.StatusId = 2;
                    bussPaymentsHist.DriverId = Convert.ToInt32(inv.DriverId);
                    bussPaymentsHist.BisnessUserPaymentHeader = inv.Id;
                    bussPaymentsHist.Iuser = Convert.ToInt32(userId);
                    await _context.BussRetPaymentsHist.AddAsync(bussPaymentsHist);
                    await _context.SaveChangesAsync();

                    //var details = await _context.BisnessUserPaymentDetails.Where(t => t.HeaderId == inv.Id).ToListAsync();

                    //var statusinv = await _context.InvoiceStatus.FindAsync(2);
                    //List<Shipment> listpay = new List<Shipment>();
                    //foreach (var item in details)
                    //{
                    //    listpay.Add(await _context.Shipments.FindAsync(Convert.ToInt32(item.ShipmentId)));
                    //}
                    //foreach (var item in listpay)
                    //{
                    //    var ship = await _context.Shipments.FindAsync(Convert.ToInt32(item.Id));
                    //    if (ship != null)
                    //    {
                    //        var newstatus = 0;
                    //        if (ship.RetStatus == true || ship.ShipmentTrackingNo.Contains("_ex"))
                    //        {
                    //            newstatus = 23;
                    //        }
                    //        else
                    //        {
                    //            newstatus = statusinv.TransferShipmentStatusTo;
                    //        }

                    //        //add Submitted status
                    //        ShipmentLog shipmentLog = new ShipmentLog();
                    //        shipmentLog.ShipmentId = ship.Id;
                    //        shipmentLog.EntryDate = DateTime.Now;
                    //        shipmentLog.EntryDateTine = DateTime.Now;
                    //        shipmentLog.UserId = Convert.ToInt32(userId);
                    //        shipmentLog.Status = newstatus;
                    //        shipmentLog.ClientName = ship.ClientName;
                    //        shipmentLog.ClientPhone = ship.ClientPhone;
                    //        shipmentLog.FromCityId = ship.FromCityId;
                    //        shipmentLog.ClientCityId = ship.ClientCityId;
                    //        shipmentLog.ClientAreaId = ship.ClientAreaId;
                    //        shipmentLog.IsUserBusiness = ship.IsUserBusiness;
                    //        shipmentLog.SenderName = ship.SenderName;
                    //        shipmentLog.SenderTel = ship.SenderTel;
                    //        shipmentLog.BusinessUserId = ship.BusinessUserId;
                    //        await _context.ShipmentLogs.AddAsync(shipmentLog);
                    //        await _context.SaveChangesAsync();

                    //        SessionAddRemark sessionAddRemark = new SessionAddRemark();
                    //        sessionAddRemark.ShipmentId = ship.Id;
                    //        sessionAddRemark.UserId = Convert.ToInt32(userId);
                    //        sessionAddRemark.EntryDateTime = DateTime.Now;
                    //        sessionAddRemark.OldStatus = ship.Status;
                    //        sessionAddRemark.NewStatus = newstatus;
                    //        sessionAddRemark.EntryDate = DateTime.Now;
                    //        ship.LastUpdate = DateTime.Now;
                    //        ship.Status = newstatus;
                    //        ship.DriverId = 0;
                    //        _context.Shipments.Update(ship);
                    //        await _context.SaveChangesAsync();
                    //        await _context.SessionAddRemarks.AddAsync(sessionAddRemark);
                    //        await _context.SaveChangesAsync();
                    //    }
                    //}
                }
                else
                {
                    msg += Environment.NewLine + $"لم يتم العثور على الفاتورة {inv.Id} ";

                }
            }

            return Json(msg);
        }

        [HttpPost]
        public async Task<IActionResult> changeDriver([FromBody] List<InvoiceBusinessUserShipments> invoiceBusinessUserShipments, int DriverId)
        {
            string userId = User.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;
            var user = await _context.Users.Where(t => t.Id == Convert.ToInt32(userId)).FirstOrDefaultAsync();
            var msg = "";
            foreach (var item1 in invoiceBusinessUserShipments)
            {
                var inv = await _context.BisnessUserPaymentHeader.Where(t => t.Id == item1.ID).FirstOrDefaultAsync();
                if (inv != null)
                {
                    inv.DriverId = DriverId;
                    _context.BisnessUserPaymentHeader.Update(inv);
                    await _context.SaveChangesAsync();
                    msg += Environment.NewLine + $"تم تغيير السائق للفاتورة {inv.Id} ";

                    BussPaymentsHist bussPaymentsHist = new BussPaymentsHist();
                    bussPaymentsHist.StatusId = Convert.ToInt32(inv.StatusId);
                    bussPaymentsHist.DriverId = Convert.ToInt32(inv.DriverId);
                    bussPaymentsHist.BisnessUserPaymentHeader = inv.Id;
                    bussPaymentsHist.Iuser = Convert.ToInt32(userId);
                    await _context.BussPaymentsHist.AddAsync(bussPaymentsHist);
                    await _context.SaveChangesAsync();
                }
            }

            return Json(msg);
        }


        [HttpPost]
        public async Task<IActionResult> changeRetDriver([FromBody] List<InvoiceBusinessUserShipments> invoiceBusinessUserShipments, int DriverId)
        {
            string userId = User.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;
            var user = await _context.Users.Where(t => t.Id == Convert.ToInt32(userId)).FirstOrDefaultAsync();
            var msg = "";
            foreach (var item1 in invoiceBusinessUserShipments)
            {
                var inv = await _context.BisnessUserReturnHeader.Where(t => t.Id == item1.ID).FirstOrDefaultAsync();
                if (inv != null)
                {
                    inv.DriverId = DriverId;
                    _context.BisnessUserReturnHeader.Update(inv);
                    await _context.SaveChangesAsync();
                    msg += Environment.NewLine + $"تم تغيير السائق للفاتورة {inv.Id} ";

                    BussRetPaymentsHist bussPaymentsHist = new BussRetPaymentsHist();
                    bussPaymentsHist.StatusId = Convert.ToInt32(inv.StatusId);
                    bussPaymentsHist.DriverId = Convert.ToInt32(inv.DriverId);
                    bussPaymentsHist.BisnessUserPaymentHeader = inv.Id;
                    bussPaymentsHist.Iuser = Convert.ToInt32(userId);
                    await _context.BussRetPaymentsHist.AddAsync(bussPaymentsHist);
                    await _context.SaveChangesAsync();
                }
            }

            return Json(msg);
        }


        public async Task<IActionResult> UrlDatasourceInvoiceBusinessUserShipments([FromBody] DataManagerRequest dm, string barcode = "")
        {
            string userId = User.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;
            var user = await _context.Users.Where(t => t.Id == Convert.ToInt32(userId)).FirstOrDefaultAsync();


            var DataSource = _context.InvoiceBusinessUserShipments.Where(t => t.StatusId == 1).AsQueryable();
            var codes = barcode.Split(",");
            if (!string.IsNullOrEmpty(barcode))
            {
                DataSource = _context.InvoiceBusinessUserShipments.Where(t => t.StatusId == 1 && codes.Contains(t.ID.ToString())).AsQueryable();
            }

            DataOperations operation = new DataOperations();
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);  //Search
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            if (dm.Where != null && dm.Where.Count > 0) //Filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
            }
            int count = DataSource.Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }
            return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);
        }

        public async Task<IActionResult> UrlDatasourceInvoiceRetBusinessUserShipments([FromBody] DataManagerRequest dm, string barcode = "", string BranchId = "")
        {
            string userId = User.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;
            var user = await _context.Users.Where(t => t.Id == Convert.ToInt32(userId)).FirstOrDefaultAsync();


            var DataSource = _context.InvoiceRetBusinessUserShipments.Where(t => t.StatusId == 3 && t.EmpNameBranchID.ToString() == BranchId).AsQueryable();
            var codes = barcode.Split(",");
            if (!string.IsNullOrEmpty(barcode))
            {
                DataSource = _context.InvoiceRetBusinessUserShipments.Where(t => t.StatusId == 3 && codes.Contains(t.ID.ToString())).AsQueryable();
            }

            DataOperations operation = new DataOperations();
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);  //Search
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            if (dm.Where != null && dm.Where.Count > 0) //Filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
            }
            int count = DataSource.Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }
            return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);
        }


        public async Task<IActionResult> UrlDatasourceRPTPaymentHistoryUser([FromBody] DataManagerRequest dm, DateTime? fromDate, DateTime? toDate, int CompanyBranches)
        {
            if (fromDate == null && toDate == null)
            {
                fromDate = DateTime.Now.AddDays(-1);
                toDate = DateTime.Now;
            }
            var DataSource = _context.RPTPaymentHistoryUsers.Where(t => t.PaymentDate >= fromDate && t.PaymentDate <= toDate.Value.AddHours(23).AddMinutes(59) && t.BranchNameId == CompanyBranches).AsQueryable();

            DataOperations operation = new DataOperations();
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);  //Search
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            if (dm.Where != null && dm.Where.Count > 0) //Filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
            }
            int count = DataSource.Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }
            return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);
        }
        public async Task<IActionResult> RptpaymentHistoryDrivers()
        {

            var user = await _accontext.Users.FirstOrDefaultAsync(t => t.UserName == User.Identity.Name);

            var br = user.DriverAccountBranchIds.Split(",");
            ViewBag.CompanyBranches = await _context.CompanyBranches.Where(t => br.Contains(t.Id.ToString())).ToListAsync();

            return View();
        }

        public async Task<IActionResult> RPTPaymentHistoryUsers()
        {
            var user = await _accontext.Users.FirstOrDefaultAsync(t => t.UserName == User.Identity.Name);

            var br = user.BusinessAccountBranchIds.Split(",");
            ViewBag.CompanyBranches = await _context.CompanyBranches.Where(t => br.Contains(t.Id.ToString())).ToListAsync();
            return View();
        }

        public async Task<IActionResult> UrlDatasourceRPTPaymentHistoryDriver([FromBody] DataManagerRequest dm, DateTime? fromDate, DateTime? toDate, int CompanyBranches)
        {
            if (fromDate == null && toDate == null)
            {
                fromDate = DateTime.Now.AddDays(-1);
                toDate = DateTime.Now;
            }
            var DataSource = _context.RptpaymentHistoryDrivers.Where(t => t.PaymentDate >= fromDate && t.PaymentDate <= toDate.Value.AddHours(23).AddMinutes(59) && t.BranchNameId == CompanyBranches).AsQueryable();

            DataOperations operation = new DataOperations();
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);  //Search
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            if (dm.Where != null && dm.Where.Count > 0) //Filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
            }
            int count = DataSource.Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }
            return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);
        }
        [Authorize(Policy = "accountmanagement.businessstatementbulk")]
        public async Task<IActionResult> BusinessStatementBulk()
        {
            ViewBag.Driver = from t1 in _context.Users
                             where t1.UserType == "4"
                             select new { t1.Id, Name = $"{t1.UserName}-{t1.FirstName} {t1.LastName}" };
            var t = from c in _context.BusinessStatementBulk

                    select new
                    {
                        Id = c.SenderId,
                        Name = c.SenderName
                    };
            ViewBag.User = await t.ToListAsync();
            ViewBag.InvoiceStatus = await _context.InvoiceStatus.ToListAsync();

            var user = await _accontext.Users.FirstOrDefaultAsync(t => t.UserName == User.Identity.Name);

            var br = user.BusinessAccountBranchIds.Split(",");
            ViewBag.CompanyBranches = await _context.CompanyBranches.Where(t => br.Contains(t.Id.ToString())).ToListAsync();

            return View();
        }
        [Authorize(Policy = "accountmanagement.businessretstatementbulk")]
        public async Task<IActionResult> BusinessRetStatementBulk()
        {
            ViewBag.Driver = from t1 in _context.Users
                             where t1.UserType == "4"
                             select new { t1.Id, Name = $"{t1.UserName}-{t1.FirstName} {t1.LastName}" };
            var t = from c in _context.BusinessStatementBulk

                    select new
                    {
                        Id = c.SenderId,
                        Name = c.SenderName
                    };
            ViewBag.User = await t.ToListAsync();
            ViewBag.InvoiceStatus = await _context.InvoiceStatus.ToListAsync();
            ViewBag.CompanyBranches = await _context.CompanyBranches.ToListAsync();
            return View();
        }


        [Authorize(Policy = "accountmanagement.driverpayment")]
        public async Task<IActionResult> DriverPayment()
        {
            var t = from c in _context.Drives
                    select new
                    {
                        Id = c.Id,
                        Name = c.FirstName + " " + c.SecoundName + " " + c.FamilyName
                    };
            ViewBag.Driver = await t.ToListAsync();
            return View();
        }

        [Authorize(Policy = "accountmanagement.userpayment")]
        public async Task<IActionResult> UserPayment()
        {
            //ViewBag.city = await _context.Cities.ToListAsync();
            var user = from u in _context.Users
                       where u.UserType == "3"
                       select new { u.Id, UserName = u.FirstName + " " + u.LastName };
            ViewBag.user = await user.ToListAsync();
            return View();
        }

        [HttpGet]
        [Authorize(Policy = "accountmanagement.printslip")]
        public IActionResult PrintSlip()
        {
            return View();
        }

        public IActionResult UrlDatasourceDriverStatment([FromBody] DataManagerRequest dm, int DriverId)
        {

            var DataSource = _context.RptDriverPay.Where(r => r.DriverId == DriverId).AsQueryable();

            DataOperations operation = new DataOperations();
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);  //Search
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            if (dm.Where != null && dm.Where.Count > 0) //Filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
            }
            int count = DataSource.Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }
            return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);
        }

        public async Task<ActionResult> Update([FromBody] ICRUDModel<RptDriverPay> value)
        {
            var Shipments = await _context.Shipments.FindAsync(value.value.Id);
            Shipments.DriverExtraComisionValue = value.value.DriverExtraComisionValue;
            _context.Shipments.Update(Shipments);
            await _context.SaveChangesAsync();

            return Json(value.value);
        }

        public IActionResult UrlDatasourceBusnissStatment([FromBody] DataManagerRequest dm, int? userId)
        {


            //var BusnissStatment = from sh in _context.Shipments
            //                      join bus in _context.Users on sh.BusinessUserId equals bus.Id
            //                      join st in _context.ShipmentStatuses on sh.Status equals st.Id
            //                      join frmc in _context.Cities on sh.FromCityId equals frmc.Id
            //                      join toc in _context.Cities on sh.ClientCityId equals toc.Id
            //                      join sy in _context.ShipmentsTypes on sh.ShipmentTypeId equals sy.Id
            //                      join ar in _context.Areas on sh.ClientAreaId equals Convert.ToInt32(ar.Id)

            //                      where (sh.Status == Convert.ToInt32(StatusEnum.InAccounting) && sh.BusinessUserId == userId)

            //                      select new
            //                      {
            //                          sh.Id,
            //                          SenderName = bus.FirstName + " " + bus.LastName,
            //                          sh.ShipmentTrackingNo,
            //                          sh.BusinessUserId,
            //                          Status = st.ArabicDescription,
            //                          ShipmentsType = sy.Description,
            //                          FromCity = frmc.ArabicCityName,
            //                          ClientName = sh.ClientName,
            //                          ClientPhone = sh.ClientPhone,
            //                          ToCity = toc.ArabicCityName,
            //                          AreaName = ar.AreaName,
            //                          AreaDescription = ar.Description,
            //                          EntryDateTime = sh.EntryDateTime,
            //                          ShipmentTotal = Convert.ToDecimal(sh.ShipmentTotal),
            //                          ShipmentPrice = Convert.ToDecimal(sh.ShipmentPrice),
            //                          ShipmentExtraFees = Convert.ToDecimal(sh.ShipmentExtraFees),
            //                          ShipmentFees = Convert.ToDecimal(Convert.ToDecimal(sh.ShipmentFees)),
            //                          ShipmentContains = sh.ShipmentContains,
            //                          Alert = sy.Alert,
            //                      };


            var BusnissStatment = (from sh in _context.ShipmentSummary
                                   where (sh.StatusId == Convert.ToInt32(StatusEnum.InAccounting) && sh.BusinessUserID == userId)
                                   select new
                                   {
                                       Id = sh.ID,
                                       SenderName = sh.SenderName,
                                       sh.ShipmentTrackingNo,
                                       BusinessUserId = sh.BusinessUserID,
                                       Status = sh.StatusAr,
                                       ShipmentsType = sh.ShipmentsType,
                                       FromCity = sh.FromCityAr,
                                       ClientName = sh.ClientName,
                                       ClientPhone = sh.ClientPhone,
                                       ToCity = sh.ToCityAr,
                                       AreaName = sh.AreaName,
                                       AreaDescription = sh.AreaDescription,
                                       EntryDateTime = sh.EntryDateTime,
                                       ShipmentTotal = sh.ShipmentTotal,
                                       ShipmentPrice = sh.ShipmentPrice,
                                       ShipmentExtraFees = sh.ShipmentExtraFees,
                                       ShipmentFees = sh.ShipmentFees,
                                       ShipmentContains = sh.ShipmentContains,
                                       Alert = sh.Alert,
                                       sh.IUser
                                   })
                        .AsEnumerable()
                        .Select(item => new
                        {
                            item.Id,
                            item.SenderName,
                            item.ShipmentTrackingNo,
                            item.BusinessUserId,
                            item.Status,
                            item.ShipmentsType,
                            item.FromCity,
                            item.ClientName,
                            item.ClientPhone,
                            item.ToCity,
                            item.AreaName,
                            item.AreaDescription,
                            item.EntryDateTime,
                            ShipmentTotal = Convert.ToDecimal(item.ShipmentTotal),
                            ShipmentPrice = Convert.ToDecimal(item.ShipmentPrice),
                            ShipmentExtraFees = Convert.ToDecimal(item.ShipmentExtraFees),
                            ShipmentFees = Convert.ToDecimal(item.ShipmentFees),
                            item.ShipmentContains,
                            item.Alert,
                            item.IUser,
                        });
            var DataSource = BusnissStatment;

            DataOperations operation = new DataOperations();
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);  //Search
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            if (dm.Where != null && dm.Where.Count > 0) //Filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
            }
            int count = DataSource.Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }
            return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);
        }
        public IActionResult UrlDatasourceBusnissStatmentM([FromBody] DataManagerRequest dm, int? userId, int? MuserId)
        {
            var BusnissStatment = (from sh in _context.ShipmentSummary
                                   where (sh.StatusId == Convert.ToInt32(StatusEnum.InAccounting) && sh.BusinessUserID == userId && sh.UserID == MuserId)
                                   select new
                                   {
                                       Id = sh.ID,
                                       SenderName = sh.SenderName,
                                       sh.ShipmentTrackingNo,
                                       BusinessUserId = sh.BusinessUserID,
                                       Status = sh.StatusAr,
                                       ShipmentsType = sh.ShipmentsType,
                                       FromCity = sh.FromCityAr,
                                       ClientName = sh.ClientName,
                                       ClientPhone = sh.ClientPhone,
                                       ToCity = sh.ToCityAr,
                                       AreaName = sh.AreaName,
                                       AreaDescription = sh.AreaDescription,
                                       EntryDateTime = sh.EntryDateTime,
                                       ShipmentTotal = sh.ShipmentTotal,
                                       ShipmentPrice = sh.ShipmentPrice,
                                       ShipmentExtraFees = sh.ShipmentExtraFees,
                                       ShipmentFees = sh.ShipmentFees,
                                       ShipmentContains = sh.ShipmentContains,
                                       Alert = sh.Alert,
                                       sh.IUser
                                   })
                        .AsEnumerable()
                        .Select(item => new
                        {
                            item.Id,
                            item.SenderName,
                            item.ShipmentTrackingNo,
                            item.BusinessUserId,
                            item.Status,
                            item.ShipmentsType,
                            item.FromCity,
                            item.ClientName,
                            item.ClientPhone,
                            item.ToCity,
                            item.AreaName,
                            item.AreaDescription,
                            item.EntryDateTime,
                            ShipmentTotal = Convert.ToDecimal(item.ShipmentTotal),
                            ShipmentPrice = Convert.ToDecimal(item.ShipmentPrice),
                            ShipmentExtraFees = Convert.ToDecimal(item.ShipmentExtraFees),
                            ShipmentFees = Convert.ToDecimal(item.ShipmentFees),
                            item.ShipmentContains,
                            item.Alert,
                            item.IUser,
                        });
            var DataSource = BusnissStatment;

            DataOperations operation = new DataOperations();
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);  //Search
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            if (dm.Where != null && dm.Where.Count > 0) //Filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
            }
            int count = DataSource.Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }
            return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);
        }



        public IActionResult UrlDatasourceBusnissShipmentsReturn([FromBody] DataManagerRequest dm, int? userId)
        {

            var BusnissStatment = (from sh in _context.Shipments
                                   join bus in _context.Users on sh.BusinessUserId equals bus.Id into busJoin
                                   from bus in busJoin.DefaultIfEmpty()

                                   join st in _context.ShipmentStatuses on sh.Status equals st.Id into stJoin
                                   from st in stJoin.DefaultIfEmpty()

                                   join frmc in _context.Cities on sh.FromCityId equals frmc.Id into frmcJoin
                                   from frmc in frmcJoin.DefaultIfEmpty()

                                   join toc in _context.Cities on sh.ClientCityId equals toc.Id into tocJoin
                                   from toc in tocJoin.DefaultIfEmpty()

                                   join sy in _context.ShipmentsTypes on sh.ShipmentTypeId equals sy.Id into syJoin
                                   from sy in syJoin.DefaultIfEmpty()

                                   join ar in _context.Areas on sh.ClientAreaId equals Convert.ToInt32(ar.Id) into arJoin
                                   from ar in arJoin.DefaultIfEmpty()

                                   where (sh.RetToCustomer == true && sh.CustomerReceved == false && sh.BusinessUserId == userId)
                                   select new
                                   {
                                       sh.Id,
                                       SenderName = bus != null ? bus.FirstName + " " + bus.LastName : null,
                                       sh.ShipmentTrackingNo,
                                       sh.BusinessUserId,
                                       Status = st != null ? st.ArabicDescription : null,
                                       ShipmentsType = sy != null ? sy.Description : null,
                                       FromCity = frmc != null ? frmc.ArabicCityName : null,
                                       ClientName = sh.ClientName,
                                       ClientPhone = sh.ClientPhone,
                                       ToCity = toc != null ? toc.ArabicCityName : null,
                                       AreaName = ar != null ? ar.AreaName : null,
                                       AreaDescription = ar != null ? ar.Description : null,
                                       EntryDateTime = sh.EntryDateTime,
                                       ShipmentTotal = sh.ShipmentTotal,
                                       ShipmentPrice = sh.ShipmentPrice,
                                       ShipmentExtraFees = sh.ShipmentExtraFees,
                                       ShipmentFees = sh.ShipmentFees,
                                       ShipmentContains = sh.ShipmentContains,
                                       Alert = sy != null ? sy.Alert : null,
                                   })
                  .AsEnumerable()
                  .Select(item => new
                  {
                      item.Id,
                      item.SenderName,
                      item.ShipmentTrackingNo,
                      item.BusinessUserId,
                      item.Status,
                      item.ShipmentsType,
                      item.FromCity,
                      item.ClientName,
                      item.ClientPhone,
                      item.ToCity,
                      item.AreaName,
                      item.AreaDescription,
                      item.EntryDateTime,
                      ShipmentTotal = Convert.ToDecimal(item.ShipmentTotal),
                      ShipmentPrice = Convert.ToDecimal(item.ShipmentPrice),
                      ShipmentExtraFees = Convert.ToDecimal(item.ShipmentExtraFees),
                      ShipmentFees = Convert.ToDecimal(item.ShipmentFees),
                      item.ShipmentContains,
                      item.Alert,
                  });

            var DataSource = BusnissStatment;

            DataOperations operation = new DataOperations();
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);  //Search
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            if (dm.Where != null && dm.Where.Count > 0) //Filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
            }
            int count = DataSource.Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }
            return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);
        }

        public IActionResult UrlDatasourceBusinessStatementBulk([FromBody] DataManagerRequest dm, int? branchId)
        {
            var DataSource = _context.BusinessStatementBulk.Where(t => t.CompanyBranchID == branchId);

            DataOperations operation = new DataOperations();
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);  //Search
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            if (dm.Where != null && dm.Where.Count > 0) //Filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
            }
            int count = DataSource.Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }
            return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);
        }
        public IActionResult UrlDatasourceBusinessRetStatementBulk([FromBody] DataManagerRequest dm, int? branchId)
        {
            var DataSource = _context.BusinessRetStatementBulk.Where(t => t.CompanyBranchID == branchId);

            DataOperations operation = new DataOperations();
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);  //Search
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            if (dm.Where != null && dm.Where.Count > 0) //Filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
            }
            int count = DataSource.Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }
            return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);
        }

        [HttpPost]
        public async Task<IActionResult> PayToDriver([FromBody] List<RptDriverPay> rptDriverPay)
        {
            //string userId = User.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;
            //var user = await _context.Users.FindAsync(Convert.ToInt32(userId));
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            if (rptDriverPay.Count > 0)
            {


                var cashAccount = await _accontext.UserPaymentAccounts
                    .Where(u => u.UserId == user.Id && u.CurrencyId == 1)
                    .FirstOrDefaultAsync();
                if (cashAccount == null)
                {
                    return BadRequest("لا يوجد حساب صندوق مرتبط بالمستخدم الحالي");
                }

                var driverParentSetting = await _accontext.SystemSettings.FirstOrDefaultAsync(s => s.Key == "DriverParentAccountId");
                if (driverParentSetting == null)
                {
                    return BadRequest("إعدادات حساب السائق غير متوفرة");
                }

                var driverParentAccount = await _accontext.Accounts.FirstOrDefaultAsync(t => t.Code == driverParentSetting.Value);
                if (driverParentAccount == null)
                {
                    return BadRequest("الحساب الرئيسي للسائق غير موجود");
                }

                var revenueAccountSetting = await _accontext.SystemSettings.FirstOrDefaultAsync(s => s.Key == "RevenueAccountCode");
                if (revenueAccountSetting == null)
                {
                    return BadRequest("إعدادات حساب الإيرادات غير متوفرة");
                }

                var revenueAccount = await _accontext.Accounts.FirstOrDefaultAsync(t => t.Code == revenueAccountSetting.Value);
                if (revenueAccount == null)
                {
                    return BadRequest("حساب الإيرادات غير موجود");
                }

                var driverPaymentHeader = new DriverPaymentHeader();
                var driverPayments = new List<DriverPaymentDetail>();
                await _context.DriverPaymentHeader.AddAsync(driverPaymentHeader);
                await _context.SaveChangesAsync();

                var listpay = new List<RptDriverPay>();
                foreach (var item in rptDriverPay)
                {
                    var shipmentsPay = await _context.RptDriverPay.Where(t => t.Id == Convert.ToInt64(item.Id)).FirstOrDefaultAsync();
                    if (shipmentsPay != null)
                    {
                        driverPayments.Add(new DriverPaymentDetail
                        {
                            HeaderId = driverPaymentHeader.Id,
                            ComisionValue = shipmentsPay.CommissionPerItem,
                            ShipmentId = shipmentsPay.ShipmentId,
                            ShipmentTrackingNo = shipmentsPay.ShipmentTrackingNo,
                            DriverExtraComisionValue = shipmentsPay.DriverExtraComisionValue,
                            CompanyRevenueValue = shipmentsPay.ShipmentCod - shipmentsPay.ShipmentPrice,
                        });
                        listpay.Add(shipmentsPay);
                    }
                }

                var driverAccount = await EnsureDriverAccountAsync(listpay, driverParentAccount);
                if (driverAccount == null)
                {
                    return BadRequest("تعذر تحديد حساب السائق");
                }

                var customerParentSetting = await _accontext.SystemSettings.FirstOrDefaultAsync(s => s.Key == "CustomerParentAccountId");
                if (customerParentSetting == null)
                {
                    return BadRequest("إعدادات حساب العميل غير متوفرة");
                }

                var customerParentAccount = await _accontext.Accounts.FirstOrDefaultAsync(t => t.Code == customerParentSetting.Value);
                if (customerParentAccount == null)
                {
                    return BadRequest("الحساب الرئيسي للعميل غير موجود");
                }


                await _context.DriverPaymentDetails.AddRangeAsync(driverPayments);
                await _context.SaveChangesAsync();
                driverPaymentHeader.PaymentValue = listpay.Sum(t => t.CommissionPerItem) + listpay.Sum(t => t.DriverExtraComisionValue);
                driverPaymentHeader.SumOfComison = listpay.Sum(t => t.CommissionPerItem) + listpay.Sum(t => t.DriverExtraComisionValue);
                driverPaymentHeader.TotalCod = listpay.Sum(t => t.ShipmentTotal) - (listpay.Sum(t => t.CommissionPerItem) + listpay.Sum(t => t.DriverExtraComisionValue));
                driverPaymentHeader.DriverId = listpay.DistinctBy(t => t.DriverId).FirstOrDefault().DriverId;
                driverPaymentHeader.PaymentDate = DateTime.Now;
                driverPaymentHeader.EntryUserId = 0;// Convert.ToInt32(user.Id);
                _context.DriverPaymentHeader.Update(driverPaymentHeader);
                await _context.SaveChangesAsync();


                var customerAccountsCache = new Dictionary<int, Account>();

                foreach (var item in listpay)
                {
                    var id1 = item.ShipmentId;

                    var sh = await _context.Shipments.FirstOrDefaultAsync(t => t.Id == Convert.ToInt32(id1));

                    var area = await _context.Areas.FirstOrDefaultAsync(t => t.Id == sh.ClientAreaId);

                    Agent? agent = null;
                    Account? agentAccount = null;
                    if (user.AgentId.HasValue)
                    {
                        agent = await _accontext.Agents.FirstOrDefaultAsync(s => s.Id == user.AgentId.Value);
                        if (agent?.AccountId != null)
                        {
                            agentAccount = await _accontext.Accounts.FirstOrDefaultAsync(t => t.Id == agent.AccountId);
                        }
                    }

                    var customerUser = await _context.Users.FirstOrDefaultAsync(t => t.Id == Convert.ToInt32(sh.BusinessUserId));
                    if (customerUser == null)
                    {
                        continue;
                    }

                    var Accbrn = await _accontext.Branches.FirstOrDefaultAsync(t => t.Code == customerUser.CompanyBranchId.ToString());
                    if (Accbrn == null)
                    {
                        continue;
                    }

                    var customerAccount = await EnsureCustomerAccountAsync(customerUser, customerAccountsCache, customerParentAccount);
                    if (customerAccount == null)
                    {
                        return BadRequest("تعذر تحديد حساب العميل");
                    }

                    var Paytxn = item;
                    var lines = new List<JournalEntryLine>();



                    #region CashAccounts txn
                    var tottxn = new JournalEntryLine();
                    tottxn.AccountId = cashAccount.AccountId;
                    tottxn.DebitAmount = 0;
                    tottxn.CreditAmount = 0;
                    if (Convert.ToDecimal(Paytxn.ShipmentTotal) <= 0)
                    {

                        tottxn.CreditAmount = Convert.ToDecimal(Paytxn.ShipmentTotal) * -1;

                    }
                    else
                    {
                        tottxn.DebitAmount = Convert.ToDecimal(Paytxn.ShipmentTotal);
                    }
                    tottxn.Reference = driverPaymentHeader.Id.ToString();
                    tottxn.Description = $"قبض من سائق مبلغ تحصيل {sh.ShipmentTrackingNo}";
                    lines.Add(tottxn);
                    #endregion

                    #region DriverAttxn txn
                    var DriverAttxn = new JournalEntryLine();
                    DriverAttxn.AccountId = driverAccount.Id;
                    DriverAttxn.CreditAmount = Convert.ToDecimal(Paytxn.CommissionPerItem);
                    DriverAttxn.Reference = driverPaymentHeader.Id.ToString();
                    DriverAttxn.Description = $"ذمة سائق عمولة {sh.ShipmentTrackingNo}";

                    lines.Add(DriverAttxn);
                    #endregion

                    #region CustomerAccounttxn txn
                    var CustomerAccounttxn = new JournalEntryLine();
                    CustomerAccounttxn.AccountId = customerAccount.Id;
                    CustomerAccounttxn.DebitAmount = 0;
                    CustomerAccounttxn.CreditAmount = 0;
                    if (Convert.ToDecimal(Paytxn.ShipmentPrice) <= 0)
                    {
                        CustomerAccounttxn.DebitAmount = Convert.ToDecimal(Paytxn.ShipmentPrice) * -1;
                    }
                    else
                    {
                        CustomerAccounttxn.CreditAmount = Convert.ToDecimal(Paytxn.ShipmentPrice);
                    }
                    CustomerAccounttxn.Reference = driverPaymentHeader.Id.ToString();
                    CustomerAccounttxn.Description = $"ذمة مورد مبلغ تحصيل {sh.ShipmentTrackingNo}";
                    lines.Add(CustomerAccounttxn);
                    #endregion



                    if (area?.CommissionBranch > 0)
                    {
                        var rev = Convert.ToDecimal((Paytxn.ShipmentTotal - Paytxn.ShipmentPrice) - area.CommissionBranch);

                        var AgenAmt = Convert.ToDecimal(area.CommissionBranch) - Convert.ToDecimal(Paytxn.CommissionPerItem);

                        #region RevenueAccounttxn txn

                        var RevenueAccounttxn = new JournalEntryLine();
                        RevenueAccounttxn.AccountId = revenueAccount.Id;
                        RevenueAccounttxn.DebitAmount = 0;
                        RevenueAccounttxn.CreditAmount = 0;
                        if (rev <= 0)
                        {
                            RevenueAccounttxn.DebitAmount = rev * -1;
                        }
                        else
                        {
                            RevenueAccounttxn.CreditAmount = rev;
                        }
                        RevenueAccounttxn.Reference = driverPaymentHeader.Id.ToString();
                        RevenueAccounttxn.Description = $"ايراد خدمة  {sh.ShipmentTrackingNo}";
                        lines.Add(RevenueAccounttxn);

                        #endregion


                        if (agentAccount != null)
                        {
                            #region agent Accounttxn txn

                            var agentAccounttxn = new JournalEntryLine();
                            agentAccounttxn.AccountId = agentAccount.Id;
                            agentAccounttxn.DebitAmount = 0;
                            agentAccounttxn.CreditAmount = 0;
                            if (AgenAmt <= 0)
                            {
                                agentAccounttxn.DebitAmount = AgenAmt * -1;
                            }
                            else
                            {
                                agentAccounttxn.CreditAmount = AgenAmt;
                            }
                            agentAccounttxn.Reference = driverPaymentHeader.Id.ToString();
                            agentAccounttxn.Description = $"عمولة وكيل  {sh.ShipmentTrackingNo}";
                            lines.Add(agentAccounttxn);

                            #endregion
                        }

                    }

                    else
                    {
                        #region RevenueAccounttxn txn
                        var RevenueAccounttxn = new JournalEntryLine();
                        RevenueAccounttxn.AccountId = revenueAccount.Id;
                        RevenueAccounttxn.DebitAmount = 0;
                        RevenueAccounttxn.CreditAmount = 0;
                        if (Convert.ToDecimal(Paytxn.ShipmentCod - Paytxn.ShipmentPrice) <= 0)
                        {
                            RevenueAccounttxn.DebitAmount = Convert.ToDecimal(Paytxn.ShipmentCod - Paytxn.ShipmentPrice) * -1;
                        }
                        else
                        {
                            RevenueAccounttxn.CreditAmount = Convert.ToDecimal(Paytxn.ShipmentCod - Paytxn.ShipmentPrice);
                        }
                        RevenueAccounttxn.Reference = driverPaymentHeader.Id.ToString();
                        RevenueAccounttxn.Description = $"ايراد خدمة  {sh.ShipmentTrackingNo}";
                        lines.Add(RevenueAccounttxn);
                        #endregion
                    }
                    #region pay DriverAttxn txn
                    var PayDriverAttxn = new JournalEntryLine();
                    PayDriverAttxn.AccountId = driverAccount.Id;
                    PayDriverAttxn.DebitAmount = Convert.ToDecimal(Paytxn.CommissionPerItem);
                    PayDriverAttxn.Reference = driverPaymentHeader.Id.ToString();
                    PayDriverAttxn.Description = $"دفع عمولة سائق  {sh.ShipmentTrackingNo}";
                    lines.Add(PayDriverAttxn);
                    #endregion

                    #region pay DriverAttxn CashAccounts txn
                    var PayCashAccountsDriverAttxn = new JournalEntryLine();
                    PayCashAccountsDriverAttxn.AccountId = cashAccount.AccountId;
                    PayCashAccountsDriverAttxn.CreditAmount = Convert.ToDecimal(Paytxn.CommissionPerItem);
                    PayCashAccountsDriverAttxn.Reference = driverPaymentHeader.Id.ToString();
                    PayCashAccountsDriverAttxn.Description = $"دفع عمولة سائق  {sh.ShipmentTrackingNo}";
                    lines.Add(PayCashAccountsDriverAttxn);
                    #endregion

                    await _journalEntryService.CreateJournalEntryAsync(
                        DateTime.Now,
                        "DriverInvoice_" + driverPaymentHeader.Id + "_" + sh.ShipmentTrackingNo,
                        Accbrn.Id,
                        user.Id,
                        lines,
                        JournalEntryStatus.Posted,
                        reference: $"DriverInvoice:{driverPaymentHeader.Id}");

                }




                foreach (var item in listpay)
                {
                    var ship = await _context.Shipments.FindAsync(Convert.ToInt32(item.ShipmentId));
                    if (ship != null)
                    {

                        //add Submitted status
                        ShipmentLog shipmentLog = new ShipmentLog();
                        shipmentLog.ShipmentId = ship.Id;
                        shipmentLog.EntryDate = DateTime.Now;
                        shipmentLog.EntryDateTine = DateTime.Now;
                        shipmentLog.UserId = 0;// Convert.ToInt32(userId);
                        shipmentLog.Status = ship.Status;
                        shipmentLog.ClientName = ship.ClientName;
                        shipmentLog.ClientPhone = ship.ClientPhone;
                        shipmentLog.FromCityId = ship.FromCityId;
                        shipmentLog.ClientCityId = ship.ClientCityId;
                        shipmentLog.ClientAreaId = ship.ClientAreaId;
                        shipmentLog.IsUserBusiness = ship.IsUserBusiness;
                        shipmentLog.SenderName = ship.SenderName;
                        shipmentLog.SenderTel = ship.SenderTel;
                        shipmentLog.BusinessUserId = ship.BusinessUserId;
                        shipmentLog.Status = Convert.ToInt32(StatusEnum.InAccounting);
                        await _context.ShipmentLogs.AddAsync(shipmentLog);
                        await _context.SaveChangesAsync();
                        SessionAddRemark sessionAddRemark = new SessionAddRemark();
                        sessionAddRemark.ShipmentId = ship.Id;
                        sessionAddRemark.UserId = 0;// Convert.ToInt32(userId);
                        sessionAddRemark.EntryDateTime = DateTime.Now;
                        sessionAddRemark.OldStatus = ship.Status;
                        sessionAddRemark.NewStatus = Convert.ToInt32(StatusEnum.InAccounting);
                        sessionAddRemark.EntryDate = DateTime.Now;
                        ship.LastUpdate = DateTime.Now;
                        ship.Status = Convert.ToInt32(StatusEnum.InAccounting); ;
                        ship.DriverId = 0;
                        _context.Shipments.Update(ship);
                        await _context.SaveChangesAsync();
                        await _context.Session_Add_Remarks.AddAsync(sessionAddRemark);
                        await _context.SaveChangesAsync();

                    }
                }
                return Ok(driverPaymentHeader);
            }

            return Ok();
        }



        //[HttpGet]
        //public async Task<IActionResult> PayToDriver2()
        //{
        //    //string userId = User.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;
        //    //var user = await _context.Users.FindAsync(Convert.ToInt32(userId));
        //    SqlConnection con = new SqlConnection("Data Source=172.16.200.98;Initial Catalog=RoadDbProd;User Id=roadfn;Password=@adrajod!e#ln1klJ*$P;MultipleActiveResultSets=true;TrustServerCertificate=True;");

        //    SqlCommand cmd = new SqlCommand("select * from migracc s\r\nwhere s.ShipmentTrackingNo not in (\r\nSELECT  substring(Description,21,2222222)\r\n  FROM [AccountingSystemDbProd].[dbo].[JournalEntries]\r\n\r\n)", con);
        //    cmd.CommandType = CommandType.Text;
        //    DataTable dataTable = new DataTable();

        //    SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(cmd);
        //    sqlDataAdapter.Fill(dataTable);

        //    foreach (DataRow DTitem in dataTable.Rows)
        //    {

        //        List<RptDriverPay> rptDriverPay = new List<RptDriverPay>();
        //        RptDriverPay rptDriverPay1 = new RptDriverPay();
        //        rptDriverPay1.Id = Convert.ToInt32(DTitem["ID"].ToString());
        //        rptDriverPay1.ShipmentTrackingNo = DTitem["ShipmentTrackingNo"].ToString();
        //        rptDriverPay1.ShipmentId = Convert.ToInt64(DTitem["ID"].ToString());
        //        rptDriverPay1.EntryDate = Convert.ToDateTime(DTitem["EntryDate"].ToString());
        //        rptDriverPay1.DriverName = DTitem["DriverName"].ToString();
        //        rptDriverPay1.ClientName = DTitem["bussName"].ToString();
        //        rptDriverPay1.CityName = "";
        //        rptDriverPay1.AreaName = "";
        //        rptDriverPay1.ShipmentTotal = Convert.ToDecimal(DTitem["ShipmentTotal"].ToString());
        //        rptDriverPay1.OldStatus = 0;
        //        rptDriverPay1.NewStatus = 0;
        //        rptDriverPay1.CommissionPerItem = Convert.ToDecimal(DTitem["DeiverCommission"].ToString());
        //        rptDriverPay1.ShipmentPrice = Convert.ToDecimal(DTitem["supplierCash"].ToString());
        //        rptDriverPay1.PaidAmountFromShipmentFees = 0;
        //        rptDriverPay1.DriverExtraComisionValue = 0;
        //        rptDriverPay1.ShipmentCod = Convert.ToDecimal(DTitem["TotalCashCOD"].ToString());
        //        rptDriverPay1.ShipmentExtraFees = 0;
        //        rptDriverPay1.DriverId = Convert.ToInt32(DTitem["DriverID"].ToString());

        //        rptDriverPay.Add(rptDriverPay1);


        //        var user = await _userManager.FindByEmailAsync(DTitem["UserName"].ToString());
        //        if (user == null)
        //        {
        //            return Unauthorized();
        //        }

        //        if (rptDriverPay.Count > 0)
        //        {


        //            var cashAccount = await _accontext.UserPaymentAccounts
        //                .Where(u => u.UserId == user.Id && u.CurrencyId == 1)
        //                .FirstOrDefaultAsync();
        //            if (cashAccount == null)
        //            {
        //                return BadRequest("لا يوجد حساب صندوق مرتبط بالمستخدم الحالي");
        //            }

        //            var driverParentSetting = await _accontext.SystemSettings.FirstOrDefaultAsync(s => s.Key == "DriverParentAccountId");
        //            if (driverParentSetting == null)
        //            {
        //                return BadRequest("إعدادات حساب السائق غير متوفرة");
        //            }

        //            var driverParentAccount = await _accontext.Accounts.FirstOrDefaultAsync(t => t.Code == driverParentSetting.Value);
        //            if (driverParentAccount == null)
        //            {
        //                return BadRequest("الحساب الرئيسي للسائق غير موجود");
        //            }

        //            var revenueAccountSetting = await _accontext.SystemSettings.FirstOrDefaultAsync(s => s.Key == "RevenueAccountCode");
        //            if (revenueAccountSetting == null)
        //            {
        //                return BadRequest("إعدادات حساب الإيرادات غير متوفرة");
        //            }

        //            var revenueAccount = await _accontext.Accounts.FirstOrDefaultAsync(t => t.Code == revenueAccountSetting.Value);
        //            if (revenueAccount == null)
        //            {
        //                return BadRequest("حساب الإيرادات غير موجود");
        //            }

        //            var driverPaymentHeader = new DriverPaymentHeader();
        //            var driverPayments = new List<DriverPaymentDetail>();
        //            driverPaymentHeader = await _context.DriverPaymentHeader.FirstOrDefaultAsync(t => t.Id == Convert.ToInt64(DTitem["HeaderID"].ToString()));

        //            var listpay = new List<RptDriverPay>();
        //            foreach (var item in rptDriverPay)
        //            {
        //                var shipmentsPay = rptDriverPay.Where(t => t.Id == Convert.ToInt64(item.Id)).FirstOrDefault();
        //                if (shipmentsPay != null)
        //                {
        //                    driverPayments.Add(new DriverPaymentDetail
        //                    {
        //                        HeaderId = driverPaymentHeader.Id,
        //                        ComisionValue = shipmentsPay.CommissionPerItem,
        //                        ShipmentId = shipmentsPay.ShipmentId,
        //                        ShipmentTrackingNo = shipmentsPay.ShipmentTrackingNo,
        //                        DriverExtraComisionValue = shipmentsPay.DriverExtraComisionValue,
        //                        CompanyRevenueValue = shipmentsPay.ShipmentCod - shipmentsPay.ShipmentPrice,
        //                    });
        //                    listpay.Add(shipmentsPay);
        //                }
        //            }

        //            var driverAccount = await EnsureDriverAccountAsync(listpay, driverParentAccount);
        //            if (driverAccount == null)
        //            {
        //                return BadRequest("تعذر تحديد حساب السائق");
        //            }

        //            var customerParentSetting = await _accontext.SystemSettings.FirstOrDefaultAsync(s => s.Key == "CustomerParentAccountId");
        //            if (customerParentSetting == null)
        //            {
        //                return BadRequest("إعدادات حساب العميل غير متوفرة");
        //            }

        //            var customerParentAccount = await _accontext.Accounts.FirstOrDefaultAsync(t => t.Code == customerParentSetting.Value);
        //            if (customerParentAccount == null)
        //            {
        //                return BadRequest("الحساب الرئيسي للعميل غير موجود");
        //            }
        //            var customerAccountsCache = new Dictionary<int, Account>();

        //            foreach (var item in listpay)
        //            {
        //                var id1 = item.ShipmentId;

        //                var sh = await _context.Shipments.FirstOrDefaultAsync(t => t.Id == Convert.ToInt32(id1));

        //                var area = await _context.Areas.FirstOrDefaultAsync(t => t.Id == sh.ClientAreaId);

        //                Agent? agent = null;
        //                Account? agentAccount = null;
        //                if (user.AgentId.HasValue)
        //                {
        //                    agent = await _accontext.Agents.FirstOrDefaultAsync(s => s.Id == user.AgentId.Value);
        //                    if (agent?.AccountId != null)
        //                    {
        //                        agentAccount = await _accontext.Accounts.FirstOrDefaultAsync(t => t.Id == agent.AccountId);
        //                    }
        //                }

        //                var customerUser = await _context.Users.FirstOrDefaultAsync(t => t.Id == Convert.ToInt32(sh.BusinessUserId));
        //                if (customerUser == null)
        //                {
        //                    continue;
        //                }

        //                var Accbrn = await _accontext.Branches.FirstOrDefaultAsync(t => t.Code == customerUser.CompanyBranchId.ToString());
        //                if (Accbrn == null)
        //                {
        //                    continue;
        //                }

        //                var customerAccount = await EnsureCustomerAccountAsync(customerUser, customerAccountsCache, customerParentAccount);
        //                if (customerAccount == null)
        //                {
        //                    return BadRequest("تعذر تحديد حساب العميل");
        //                }

        //                var Paytxn = item;
        //                var lines = new List<JournalEntryLine>();



        //                #region CashAccounts txn
        //                var tottxn = new JournalEntryLine();
        //                tottxn.AccountId = cashAccount.AccountId;
        //                tottxn.DebitAmount = 0;
        //                tottxn.CreditAmount = 0;
        //                if (Convert.ToDecimal(Paytxn.ShipmentTotal) <= 0)
        //                {

        //                    tottxn.CreditAmount = Convert.ToDecimal(Paytxn.ShipmentTotal) * -1;

        //                }
        //                else
        //                {
        //                    tottxn.DebitAmount = Convert.ToDecimal(Paytxn.ShipmentTotal);
        //                }
        //                tottxn.Reference = driverPaymentHeader.Id.ToString();
        //                tottxn.Description = $"قبض من سائق مبلغ تحصيل {sh.ShipmentTrackingNo}";
        //                lines.Add(tottxn);
        //                #endregion

        //                #region DriverAttxn txn
        //                var DriverAttxn = new JournalEntryLine();
        //                DriverAttxn.AccountId = driverAccount.Id;
        //                DriverAttxn.CreditAmount = Convert.ToDecimal(Paytxn.CommissionPerItem);
        //                DriverAttxn.Reference = driverPaymentHeader.Id.ToString();
        //                DriverAttxn.Description = $"ذمة سائق عمولة {sh.ShipmentTrackingNo}";

        //                lines.Add(DriverAttxn);
        //                #endregion

        //                #region CustomerAccounttxn txn
        //                var CustomerAccounttxn = new JournalEntryLine();
        //                CustomerAccounttxn.AccountId = customerAccount.Id;
        //                CustomerAccounttxn.DebitAmount = 0;
        //                CustomerAccounttxn.CreditAmount = 0;
        //                if (Convert.ToDecimal(Paytxn.ShipmentPrice) <= 0)
        //                {
        //                    CustomerAccounttxn.DebitAmount = Convert.ToDecimal(Paytxn.ShipmentPrice) * -1;
        //                }
        //                else
        //                {
        //                    CustomerAccounttxn.CreditAmount = Convert.ToDecimal(Paytxn.ShipmentPrice);
        //                }
        //                CustomerAccounttxn.Reference = driverPaymentHeader.Id.ToString();
        //                CustomerAccounttxn.Description = $"ذمة مورد مبلغ تحصيل {sh.ShipmentTrackingNo}";
        //                lines.Add(CustomerAccounttxn);
        //                #endregion




        //                #region RevenueAccounttxn txn
        //                var RevenueAccounttxn = new JournalEntryLine();
        //                RevenueAccounttxn.AccountId = revenueAccount.Id;
        //                RevenueAccounttxn.DebitAmount = 0;
        //                RevenueAccounttxn.CreditAmount = 0;
        //                if (Convert.ToDecimal(Paytxn.ShipmentCod - Paytxn.ShipmentPrice) <= 0)
        //                {
        //                    RevenueAccounttxn.DebitAmount = Convert.ToDecimal(Paytxn.ShipmentCod - Paytxn.ShipmentPrice) * -1;
        //                }
        //                else
        //                {
        //                    RevenueAccounttxn.CreditAmount = Convert.ToDecimal(Paytxn.ShipmentCod - Paytxn.ShipmentPrice);
        //                }
        //                RevenueAccounttxn.Reference = driverPaymentHeader.Id.ToString();
        //                RevenueAccounttxn.Description = $"ايراد خدمة  {sh.ShipmentTrackingNo}";
        //                lines.Add(RevenueAccounttxn);
        //                #endregion

        //                #region pay DriverAttxn txn
        //                var PayDriverAttxn = new JournalEntryLine();
        //                PayDriverAttxn.AccountId = driverAccount.Id;
        //                PayDriverAttxn.DebitAmount = Convert.ToDecimal(Paytxn.CommissionPerItem);
        //                PayDriverAttxn.Reference = driverPaymentHeader.Id.ToString();
        //                PayDriverAttxn.Description = $"دفع عمولة سائق  {sh.ShipmentTrackingNo}";
        //                lines.Add(PayDriverAttxn);
        //                #endregion

        //                #region pay DriverAttxn CashAccounts txn
        //                var PayCashAccountsDriverAttxn = new JournalEntryLine();
        //                PayCashAccountsDriverAttxn.AccountId = cashAccount.AccountId;
        //                PayCashAccountsDriverAttxn.CreditAmount = Convert.ToDecimal(Paytxn.CommissionPerItem);
        //                PayCashAccountsDriverAttxn.Reference = driverPaymentHeader.Id.ToString();
        //                PayCashAccountsDriverAttxn.Description = $"دفع عمولة سائق  {sh.ShipmentTrackingNo}";
        //                lines.Add(PayCashAccountsDriverAttxn);
        //                #endregion

        //                await _journalEntryService.CreateJournalEntryAsync(
        //                    DateTime.Now,
        //                    "DriverInvoice_" + driverPaymentHeader.Id + "_" + sh.ShipmentTrackingNo,
        //                    Accbrn.Id,
        //                    user.Id,
        //                    lines,
        //                    JournalEntryStatus.Posted,
        //                    reference: $"DriverInvoice:{driverPaymentHeader.Id}");

        //            }
        //        }

        //    }
        //    return Ok();
        //}



        [HttpPost]
        public async Task<IActionResult> PayToBusniss([FromBody] List<PayToBus> PayToBus, int dariverID)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var statusinv = await _context.InvoiceStatus.FindAsync(1);
            if (statusinv == null)
                return Ok();

            var loginUserId = int.TryParse(user.Id, out var parsedUserId) ? parsedUserId : 0;
            var (actionResult, header) = await ProcessBusinessPaymentAsync(PayToBus, statusinv, dariverID, user, loginUserId);
            if (actionResult != null)
            {
                return actionResult;
            }

            return Ok(header);
        }

        private async Task<(IActionResult? Result, BisnessUserPaymentHeader? Header)> ProcessBusinessPaymentAsync(
            List<PayToBus> payToBus,
            InvoiceStatus statusinv,
            int driverId,
            User user,
            int loginUserId)
        {
            if (payToBus == null || payToBus.Count == 0)
            {
                return (Ok(), null);
            }

            var cashAccount = await _accontext.UserPaymentAccounts
                             .Where(u => u.UserId == user.Id && u.CurrencyId == 1)
                             .FirstOrDefaultAsync();
            if (cashAccount == null)
            {
                return (BadRequest("لا يوجد حساب صندوق مرتبط بالمستخدم الحالي"), null);
            }

            var bisnessUserPaymentHeader = new BisnessUserPaymentHeader
            {
                StatusId = statusinv.Id
            };
            var bisnessUserPaymentDetail = new List<BisnessUserPaymentDetail>();
            await _context.BisnessUserPaymentHeader.AddAsync(bisnessUserPaymentHeader);
            await _context.SaveChangesAsync();
            var listpay = new List<Shipment>();
            foreach (var item in payToBus)
            {
                var shipmentsPay = await _context.Shipments.Where(t => t.Id == Convert.ToInt64(item.Id)).FirstOrDefaultAsync();
                if (shipmentsPay != null)
                {
                    bisnessUserPaymentDetail.Add(new BisnessUserPaymentDetail
                    {
                        HeaderId = bisnessUserPaymentHeader.Id,
                        ShipmentId = shipmentsPay.Id,
                        ShipmentTrackingNo = shipmentsPay.ShipmentTrackingNo
                    });
                    listpay.Add(shipmentsPay);
                }
            }

            if (!listpay.Any())
            {
                _context.BisnessUserPaymentHeader.Remove(bisnessUserPaymentHeader);
                await _context.SaveChangesAsync();
                return (Ok(), null);
            }

            var shipmentTotals = listpay.Sum(t => t.ShipmentTotal);
            var shipmentFees = listpay.Sum(t => t.ShipmentFees);
            if (shipmentTotals - shipmentFees < 0)
            {
                _context.BisnessUserPaymentHeader.Remove(bisnessUserPaymentHeader);
                await _context.SaveChangesAsync();
                return (Ok($"لايمكن دفع الفاتورة المجموع {shipmentTotals - shipmentFees}"), null);
            }

            await _context.BisnessUserPaymentDetails.AddRangeAsync(bisnessUserPaymentDetail);
            await _context.SaveChangesAsync();

            bisnessUserPaymentHeader.PaymentValue = shipmentTotals - shipmentFees - listpay.Sum(t => t.ShipmentExtraFees);
            bisnessUserPaymentHeader.LoginUserId = loginUserId;
            bisnessUserPaymentHeader.UserId = listpay.DistinctBy(t => t.BusinessUserId).FirstOrDefault().BusinessUserId;
            bisnessUserPaymentHeader.PaymentDate = DateTime.Now;
            bisnessUserPaymentHeader.DriverId = driverId;
            _context.BisnessUserPaymentHeader.Update(bisnessUserPaymentHeader);
            await _context.SaveChangesAsync();

            BussPaymentsHist bussPaymentsHist = new BussPaymentsHist();
            bussPaymentsHist.StatusId = 1;
            bussPaymentsHist.DriverId = driverId;
            bussPaymentsHist.BisnessUserPaymentHeader = bisnessUserPaymentHeader.Id;
            bussPaymentsHist.Iuser = loginUserId;
            await _context.BussPaymentsHist.AddAsync(bussPaymentsHist);
            await _context.SaveChangesAsync();

            var customerParentSetting = await _accontext.SystemSettings.FirstOrDefaultAsync(s => s.Key == "CustomerParentAccountId");
            if (customerParentSetting == null)
            {
                return (BadRequest("إعدادات حساب العميل غير متوفرة"), null);
            }

            var customerParentAccount = await _accontext.Accounts.FirstOrDefaultAsync(t => t.Code == customerParentSetting.Value);
            if (customerParentAccount == null)
            {
                return (BadRequest("الحساب الرئيسي للعميل غير موجود"), null);
            }
            var customerAccountsCache = new Dictionary<int, Account>();

            foreach (var sh in listpay)
            {
                var customerUser = await _context.Users.FirstOrDefaultAsync(t => t.Id == Convert.ToInt32(sh.BusinessUserId));
                if (customerUser == null)
                {
                    continue;
                }
                var Accbrn = await _accontext.Branches.FirstOrDefaultAsync(t => t.Code == customerUser.CompanyBranchId.ToString());
                if (Accbrn == null)
                {
                    continue;
                }

                var customerAccount = await EnsureCustomerAccountAsync(customerUser, customerAccountsCache, customerParentAccount);
                if (customerAccount == null)
                {
                    return (BadRequest("تعذر تحديد حساب العميل"), null);
                }
                var lines = new List<JournalEntryLine>();

                #region CashAccounts txn
                var tottxn = new JournalEntryLine();
                tottxn.AccountId = cashAccount.AccountId;
                tottxn.DebitAmount = 0;
                tottxn.CreditAmount = 0;
                if (Convert.ToDecimal(sh.ShipmentPrice) <= 0)
                {
                    tottxn.DebitAmount = Convert.ToDecimal(sh.ShipmentPrice) * -1;
                }
                else
                {
                    tottxn.CreditAmount = Convert.ToDecimal(sh.ShipmentPrice);
                }
                tottxn.Reference = bisnessUserPaymentHeader.Id.ToString();
                tottxn.Description = sh.ShipmentTrackingNo + "دفع ذمة مورد ";
                lines.Add(tottxn);
                #endregion


                #region CustomerAccounttxn txn
                var CustomerAccounttxn = new JournalEntryLine();
                CustomerAccounttxn.AccountId = customerAccount.Id;
                CustomerAccounttxn.DebitAmount = 0;
                CustomerAccounttxn.CreditAmount = 0;
                if (Convert.ToDecimal(sh.ShipmentPrice) <= 0)
                {
                    CustomerAccounttxn.CreditAmount = Convert.ToDecimal(sh.ShipmentPrice) * -1;
                }
                else
                {
                    CustomerAccounttxn.DebitAmount = Convert.ToDecimal(sh.ShipmentPrice);
                }
                CustomerAccounttxn.Reference = bisnessUserPaymentHeader.Id.ToString();
                CustomerAccounttxn.Description = sh.ShipmentTrackingNo + "دفع ذمة مورد "; ;
                lines.Add(CustomerAccounttxn);
                #endregion
                await _journalEntryService.CreateJournalEntryAsync(
                    DateTime.Now,
                    "Business_" + bisnessUserPaymentHeader.Id,
                    Accbrn.Id,
                    user.Id,
                    lines,
                    JournalEntryStatus.Posted,
                    reference: $"PaymenToBusiness:{bisnessUserPaymentHeader.Id}");

            }


            foreach (var item in listpay)
            {
                var ship = await _context.Shipments.FindAsync(Convert.ToInt32(item.Id));
                if (ship != null)
                {

                    //add Submitted status
                    ShipmentLog shipmentLog = new ShipmentLog();
                    shipmentLog.ShipmentId = ship.Id;
                    shipmentLog.EntryDate = DateTime.Now;
                    shipmentLog.EntryDateTine = DateTime.Now;
                    shipmentLog.UserId = loginUserId;

                    shipmentLog.Status = statusinv.TransferShipmentStatusTo;


                    shipmentLog.ClientName = ship.ClientName;
                    shipmentLog.ClientPhone = ship.ClientPhone;
                    shipmentLog.FromCityId = ship.FromCityId;
                    shipmentLog.ClientCityId = ship.ClientCityId;
                    shipmentLog.ClientAreaId = ship.ClientAreaId;
                    shipmentLog.IsUserBusiness = ship.IsUserBusiness;
                    shipmentLog.SenderName = ship.SenderName;
                    shipmentLog.SenderTel = ship.SenderTel;
                    shipmentLog.BusinessUserId = ship.BusinessUserId;
                    shipmentLog.Status = statusinv.TransferShipmentStatusTo;
                    await _context.ShipmentLogs.AddAsync(shipmentLog);
                    await _context.SaveChangesAsync();

                    SessionAddRemark sessionAddRemark = new SessionAddRemark();
                    sessionAddRemark.ShipmentId = ship.Id;
                    sessionAddRemark.UserId = loginUserId;
                    sessionAddRemark.EntryDateTime = DateTime.Now;
                    sessionAddRemark.OldStatus = ship.Status;

                    sessionAddRemark.NewStatus = statusinv.TransferShipmentStatusTo;

                    sessionAddRemark.EntryDate = DateTime.Now;
                    ship.LastUpdate = DateTime.Now;

                    ship.Status = statusinv.TransferShipmentStatusTo;


                    ship.DriverId = 0;
                    _context.Shipments.Update(ship);
                    await _context.SaveChangesAsync();
                    await _context.Session_Add_Remarks.AddAsync(sessionAddRemark);
                    await _context.SaveChangesAsync();
                }
            }

            return (null, bisnessUserPaymentHeader);
        }

        private async Task<Account?> EnsureDriverAccountAsync1(int driverId, Account driverParentAccount)
        {


            var driver = await _context.Drives.FirstOrDefaultAsync(t => t.Id == driverId);
            if (driver == null)
            {
                return null;
            }

            var mapping = await _accontext.DriverMappingAccounts.FirstOrDefaultAsync(t => t.DriverId == driver.Id.ToString());
            if (mapping != null)
            {
                var mappedAccount = await _accontext.Accounts.FirstOrDefaultAsync(t => t.Code == mapping.AccountCode);
                if (mappedAccount != null)
                {
                    return mappedAccount;
                }
            }

            var accountName = $"{driver.Id}_{(driver.FirstName ?? string.Empty)} {(driver.FamilyName ?? string.Empty)} {(driver.Phone1 ?? string.Empty)}".Trim();
            var (accountId, _) = await _accountService.CreateAccountAsync(accountName, driverParentAccount.Id);
            var newAccount = await _accontext.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
            if (newAccount == null)
            {
                return null;
            }

            if (mapping == null)
            {
                mapping = new DriverMappingAccount
                {
                    DriverId = driver.Id.ToString(),
                    AccountId = newAccount.Id.ToString(),
                    AccountCode = newAccount.Code,
                };
                await _accontext.DriverMappingAccounts.AddAsync(mapping);
            }
            else
            {
                mapping.AccountId = newAccount.Id.ToString();
                mapping.AccountCode = newAccount.Code;
                _accontext.DriverMappingAccounts.Update(mapping);
            }

            await _accontext.SaveChangesAsync();
            return newAccount;
        }
        private async Task<Account?> EnsureDriverAccountAsync(IEnumerable<RptDriverPay> driverPayments, Account driverParentAccount)
        {
            var driverId = driverPayments.FirstOrDefault(p => p.DriverId.HasValue)?.DriverId;
            if (!driverId.HasValue)
            {
                return null;
            }

            var driver = await _context.Drives.FirstOrDefaultAsync(t => t.Id == driverId.Value);
            if (driver == null)
            {
                return null;
            }

            var mapping = await _accontext.DriverMappingAccounts.FirstOrDefaultAsync(t => t.DriverId == driver.Id.ToString());
            if (mapping != null)
            {
                var mappedAccount = await _accontext.Accounts.FirstOrDefaultAsync(t => t.Code == mapping.AccountCode);
                if (mappedAccount != null)
                {
                    return mappedAccount;
                }
            }

            var accountName = $"{driver.Id}_{(driver.FirstName ?? string.Empty)} {(driver.FamilyName ?? string.Empty)} {(driver.Phone1 ?? string.Empty)}".Trim();
            var (accountId, _) = await _accountService.CreateAccountAsync(accountName, driverParentAccount.Id);
            var newAccount = await _accontext.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
            if (newAccount == null)
            {
                return null;
            }

            if (mapping == null)
            {
                mapping = new DriverMappingAccount
                {
                    DriverId = driver.Id.ToString(),
                    AccountId = newAccount.Id.ToString(),
                    AccountCode = newAccount.Code,
                };
                await _accontext.DriverMappingAccounts.AddAsync(mapping);
            }
            else
            {
                mapping.AccountId = newAccount.Id.ToString();
                mapping.AccountCode = newAccount.Code;
                _accontext.DriverMappingAccounts.Update(mapping);
            }

            await _accontext.SaveChangesAsync();
            return newAccount;
        }

        private async Task<Account?> EnsureCustomerAccountAsync(Roadfn.Models.User customer, Dictionary<int, Account> cache, Account customerParentAccount)
        {
            if (cache.TryGetValue(customer.Id, out var cachedAccount))
            {
                return cachedAccount;
            }

            var mapping = await _accontext.CusomerMappingAccounts.FirstOrDefaultAsync(t => t.CustomerId == customer.Id.ToString());
            if (mapping != null)
            {
                var mappedAccount = await _accontext.Accounts.FirstOrDefaultAsync(t => t.Code == mapping.AccountCode);
                if (mappedAccount != null)
                {
                    cache[customer.Id] = mappedAccount;
                    return mappedAccount;
                }
            }

            var accountName = $"{customer.Id}_{(customer.FirstName ?? string.Empty)} {(customer.LastName ?? string.Empty)} {(customer.MobileNo1 ?? string.Empty)}".Trim();
            var (accountId, _) = await _accountService.CreateAccountAsync(accountName, customerParentAccount.Id);
            var newAccount = await _accontext.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
            if (newAccount == null)
            {
                return null;
            }

            if (mapping == null)
            {
                mapping = new CusomerMappingAccount
                {
                    CustomerId = customer.Id.ToString(),
                    AccountId = newAccount.Id.ToString(),
                    AccountCode = newAccount.Code,
                };
                await _accontext.CusomerMappingAccounts.AddAsync(mapping);
            }
            else
            {
                mapping.AccountId = newAccount.Id.ToString();
                mapping.AccountCode = newAccount.Code;
                _accontext.CusomerMappingAccounts.Update(mapping);
            }

            await _accontext.SaveChangesAsync();
            cache[customer.Id] = newAccount;
            return newAccount;
        }


        [HttpPost]
        public async Task<IActionResult> ReturnToBusniss([FromBody] List<PayToBus> PayToBus, int dariverID = 0)
        {
            string userId = User.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;
            var user = await _context.Users.FindAsync(Convert.ToInt32(userId));
            var statusinv = await _context.InvoiceStatus.FindAsync(3);
            if (statusinv == null)
                return Ok();


            if (PayToBus.Count > 0)
            {
                var bisnessUserPaymentHeader = new BisnessUserReturnHeader();
                bisnessUserPaymentHeader.StatusId = statusinv.Id;
                var bisnessUserPaymentDetail = new List<BisnessUserReturnDetail>();
                await _context.BisnessUserReturnHeader.AddAsync(bisnessUserPaymentHeader);
                await _context.SaveChangesAsync();
                var listpay = new List<Shipment>();
                foreach (var item in PayToBus)
                {
                    var shipmentsPay = await _context.Shipments.Where(t => t.Id == Convert.ToInt64(item.Id)).FirstOrDefaultAsync();
                    //shipmentsPay.ShipmentPrice = shipmentsPay.ShipmentPrice + shipmentsPay.ShipmentFeesDiscount;
                    //shipmentsPay.ShipmentFees = shipmentsPay.ShipmentFees - shipmentsPay.ShipmentFeesDiscount; 

                    if (shipmentsPay != null)
                        bisnessUserPaymentDetail.Add(new BisnessUserReturnDetail
                        {
                            HeaderId = bisnessUserPaymentHeader.Id,
                            ShipmentId = shipmentsPay.Id,
                            ShipmentTrackingNo = shipmentsPay.ShipmentTrackingNo
                        });
                    listpay.Add(shipmentsPay);
                }

                await _context.BisnessUserReturnDetail.AddRangeAsync(bisnessUserPaymentDetail);
                await _context.SaveChangesAsync();

                bisnessUserPaymentHeader.PaymentValue = listpay.Sum(t => t.ShipmentTotal) - listpay.Sum(t => t.ShipmentFees) - listpay.Sum(t => t.ShipmentExtraFees);
                bisnessUserPaymentHeader.LoginUserId = Convert.ToInt32(userId);
                bisnessUserPaymentHeader.UserId = listpay.DistinctBy(t => t.BusinessUserId).FirstOrDefault().BusinessUserId;
                bisnessUserPaymentHeader.PaymentDate = DateTime.Now;
                bisnessUserPaymentHeader.DriverId = dariverID;
                _context.BisnessUserReturnHeader.Update(bisnessUserPaymentHeader);
                await _context.SaveChangesAsync();

                BussRetPaymentsHist bussPaymentsHist = new BussRetPaymentsHist();
                bussPaymentsHist.StatusId = 1;
                bussPaymentsHist.DriverId = dariverID;
                bussPaymentsHist.BisnessUserPaymentHeader = bisnessUserPaymentHeader.Id;
                bussPaymentsHist.Iuser = Convert.ToInt32(userId);
                await _context.BussRetPaymentsHist.AddAsync(bussPaymentsHist);
                await _context.SaveChangesAsync();

                foreach (var item in listpay)
                {
                    var ship = await _context.Shipments.FindAsync(Convert.ToInt32(item.Id));
                    if (ship != null)
                    {
                        ship.CustomerReceved = true;
                        _context.Shipments.Update(ship);
                        await _context.SaveChangesAsync();

                    }
                }
                return Ok(bisnessUserPaymentHeader);
            }
            return Ok();
        }



        [HttpGet]
        public async Task<IActionResult> GeneratePdfFile(string Id)
        {
            var ids = Id.Split(",");

            var ss = "";
            foreach (var item2 in ids)
            {
                if (!string.IsNullOrWhiteSpace(item2))
                {
                    var retsh = await _context.BisnessUserReturnHeader.FirstOrDefaultAsync(t => t.Id.ToString() == item2);

                    var retdet = await _context.BisnessUserReturnDetail.Where(t => t.HeaderId == retsh.Id).ToListAsync();
                    var shipmentSummaries = new List<ShipmentSummary>();
                    foreach (var item in retdet)
                    {
                        var shipment = await _context.ShipmentSummary.FindAsync(Convert.ToInt32(item.ShipmentId));
                        if (shipment != null)
                            shipmentSummaries.Add(shipment);
                    }
                    shipmentSummaries = shipmentSummaries.OrderBy(t => t.AreaName).ToList();
                    var filepath = Path.Combine(_env.WebRootPath, "ReportTemplates", "ShipmentWithBarcode", "ShipmentWithBarcodeRet.html");
                    DataTable table = new DataTable();


                    DataColumn dataColumn1 = new DataColumn($"BarCode");
                    table.Columns.Add(dataColumn1);


                    DataColumn dataColumn2 = new DataColumn($"سعر الطرد");
                    table.Columns.Add(dataColumn2);

                    DataColumn dataColumn3 = new DataColumn($"حالة الشحنه");
                    table.Columns.Add(dataColumn3);

                    DataColumn dataColumn4 = new DataColumn($"اسم المستلم");
                    table.Columns.Add(dataColumn4);

                    DataColumn dataColumn5 = new DataColumn($"مدينة المستلم");
                    table.Columns.Add(dataColumn5);

                    DataColumn dataColumn6 = new DataColumn($"منطقة المستلم");
                    table.Columns.Add(dataColumn6);

                    DataColumn dataColumn7 = new DataColumn($"رقم المستلم");
                    table.Columns.Add(dataColumn7);

                    DataColumn dataColumn8 = new DataColumn($"محتويات الطرد");
                    table.Columns.Add(dataColumn8);

                    DataColumn dataColumn9 = new DataColumn($"التاريخ");
                    table.Columns.Add(dataColumn9);

                    //DataColumn dataColumn9 = new DataColumn($"ملاحظات");
                    //table.Columns.Add(dataColumn9);

                    foreach (var item in shipmentSummaries)
                    {
                        // var enttt = await _context.EntitiesChanges.Where(t => t.TableName == "Roadfn.Models.Shipment" && t.PropName == "ShipmentPrice" && t.EntityId == item.ID.ToString()).OrderBy(t => t.Idate).FirstOrDefaultAsync();
                        var shhist = await _context.ShipmentLogGrid2s.Where(t => t.ShipmentId == item.ID).OrderByDescending(t => t.Id).FirstOrDefaultAsync();
                        DataRow dataRow = table.NewRow();
                        dataRow[0] = item.ShipmentTrackingNo;

                        //if (enttt == null)
                        //{
                        //    dataRow[1] = item.ShipmentPrice;

                        //}
                        //else
                        //{
                        //    dataRow[1] = enttt.OldValue;

                        //}
                        dataRow[1] = item.OldShipmentPrice;
                        dataRow[2] = shhist.FromStatus;
                        dataRow[3] = item.ClientName;
                        dataRow[4] = item.ToCityAr;
                        dataRow[5] = item.AreaName;
                        dataRow[6] = item.ClientPhone;
                        dataRow[7] = item.ShipmentContains;
                        dataRow[8] = item.EntryDateTime.ToString("dd/MM/yyyy");
                        table.Rows.Add(dataRow);
                    }
                    StreamReader sr = new StreamReader(filepath);
                    string s = sr.ReadToEnd();
                    s = s.Replace("{details}", ConvertToHtml3(table));
                    s = s.Replace("{Date}", DateTime.Now.ToString("dd/MM/yyyy"));
                    s = s.Replace("{Ref}", Id);
                    s = s.Replace("{sender}", shipmentSummaries.FirstOrDefault().SenderName);
                    sr.Close();

                    ss += s;
                }
            }


            ViewBag.report = ss;
            return View("ViewReport");
        }

        public string ConvertToHtml3(DataTable dt)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            //sb.AppendLine("<html>");
            //sb.AppendLine("<body>");
            sb.AppendLine("<table id='report' BORDER ='1'>");
            foreach (DataColumn dc in dt.Columns)
            {
                sb.AppendFormat("<th align = 'center'>{0}</th>", dc.ColumnName);
            }

            foreach (DataRow dr in dt.Rows)

            {
                sb.Append("<tr>");
                foreach (DataColumn dc in dt.Columns)
                {
                    if (dc.ColumnName == "BarCode")
                    {
                        string cellValue = dr[dc] != null ? dr[dc].ToString() : "";
                        sb.Append("<td><svg id='" + cellValue + "'></svg>  <script> JsBarcode('#" + cellValue + "', '" + cellValue + "', {  width: 2, height: 30,  displayValue: true });  </script>  </td>");
                    }
                    else
                    {
                        string cellValue = dr[dc] != null ? dr[dc].ToString() : "";
                        sb.AppendFormat("<td>{0}</ td>", cellValue);
                    }

                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
            //sb.AppendLine("</body>");
            //sb.AppendLine("</html>");
            return sb.ToString();
        }


        [HttpPost]
        public async Task<IActionResult> PayToBusnissBullk([FromBody] List<BusinessStatementBulk> businessStatementBulks, int dariverID)
        {
            var statusinv = await _context.InvoiceStatus.FindAsync(1);
            if (statusinv == null)
                return Ok();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var listhedar = new List<BisnessUserPaymentHeader>();

            foreach (var item1 in businessStatementBulks)
            {
                var shipments = await _context.Shipments.Where(t => t.BusinessUserId == item1.SenderId && t.Status == Convert.ToInt32(StatusEnum.InAccounting)).ToListAsync();
                if (!shipments.Any())
                {
                    continue;
                }

                var payToBus = shipments
                    .Select(item => new PayToBus { Id = item.Id, ShipmentTrackingNo = item.ShipmentTrackingNo })
                    .ToList();

                var loginUserId = int.TryParse(user.Id, out var parsedUserId) ? parsedUserId : 0;
                var (actionResult, header) = await ProcessBusinessPaymentAsync(payToBus, statusinv, dariverID, user, loginUserId);
                if (actionResult != null)
                {
                    return actionResult;
                }

                if (header != null)
                {
                    listhedar.Add(header);
                }
            }
            return Ok(listhedar);
        }

        [HttpPost]
        public async Task<IActionResult> RetBusnissBullk([FromBody] List<BusinessStatementBulk> businessStatementBulks, int dariverID)
        {
            var statusinv = await _context.InvoiceStatus.FindAsync(3);
            if (statusinv == null)
                return Ok();

            string userId = User.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;
            var user = await _context.Users.FindAsync(Convert.ToInt32(userId));

            var listhedar = new List<BisnessUserReturnHeader>();

            foreach (var item1 in businessStatementBulks)
            {
                var shipments = await _context.Shipments.Where(t => t.BusinessUserId == item1.SenderId && t.RetToCustomer == true && t.CustomerReceved == false).ToListAsync();
                List<PayToBus> PayToBus = new List<PayToBus>();

                foreach (var item in shipments)
                {
                    PayToBus.Add(new ViewModel.PayToBus { Id = item.Id, ShipmentTrackingNo = item.ShipmentTrackingNo });
                }
                if (PayToBus.Count > 0)
                {
                    var bisnessUserPaymentHeader = new BisnessUserReturnHeader();
                    bisnessUserPaymentHeader.StatusId = statusinv.Id;
                    var bisnessUserPaymentDetail = new List<BisnessUserReturnDetail>();
                    await _context.BisnessUserReturnHeader.AddAsync(bisnessUserPaymentHeader);
                    await _context.SaveChangesAsync();
                    var listpay = new List<Shipment>();
                    foreach (var item in PayToBus)
                    {
                        var shipmentsPay = await _context.Shipments.Where(t => t.Id == Convert.ToInt64(item.Id)).FirstOrDefaultAsync();
                        if (shipmentsPay != null)
                            bisnessUserPaymentDetail.Add(new BisnessUserReturnDetail
                            {
                                HeaderId = bisnessUserPaymentHeader.Id,
                                ShipmentId = shipmentsPay.Id,
                                ShipmentTrackingNo = shipmentsPay.ShipmentTrackingNo
                            });
                        listpay.Add(shipmentsPay);
                    }
                    //if (listpay.Sum(t => t.ShipmentTotal) - listpay.Sum(t => t.ShipmentFees) < 0)
                    //{
                    //    _context.BisnessUserReturnHeader.Remove(bisnessUserPaymentHeader);
                    //    await _context.SaveChangesAsync();
                    //    //return BadRequest();
                    //}
                    //else
                    //{
                    await _context.BisnessUserReturnDetail.AddRangeAsync(bisnessUserPaymentDetail);
                    await _context.SaveChangesAsync();
                    bisnessUserPaymentHeader.PaymentValue = listpay.Sum(t => t.ShipmentTotal) - listpay.Sum(t => t.ShipmentFees) - listpay.Sum(t => t.ShipmentExtraFees);
                    bisnessUserPaymentHeader.LoginUserId = Convert.ToInt32(userId);
                    bisnessUserPaymentHeader.UserId = listpay.DistinctBy(t => t.BusinessUserId).FirstOrDefault().BusinessUserId;
                    bisnessUserPaymentHeader.PaymentDate = DateTime.Now;
                    bisnessUserPaymentHeader.DriverId = dariverID;
                    _context.BisnessUserReturnHeader.Update(bisnessUserPaymentHeader);
                    await _context.SaveChangesAsync();
                    BussRetPaymentsHist bussPaymentsHist = new BussRetPaymentsHist();
                    bussPaymentsHist.StatusId = 1;
                    bussPaymentsHist.DriverId = dariverID;
                    bussPaymentsHist.BisnessUserPaymentHeader = bisnessUserPaymentHeader.Id;
                    bussPaymentsHist.Iuser = Convert.ToInt32(userId);
                    await _context.BussRetPaymentsHist.AddAsync(bussPaymentsHist);
                    await _context.SaveChangesAsync();


                    foreach (var item in listpay)
                    {
                        var ship = await _context.Shipments.FindAsync(Convert.ToInt32(item.Id));
                        if (ship != null)
                        {
                            ship.CustomerReceved = true;
                            _context.Shipments.Update(ship);
                            await _context.SaveChangesAsync();
                        }
                    }

                    listhedar.Add(bisnessUserPaymentHeader);
                    //}
                }
            }
            return Ok(listhedar);
        }


        [HttpGet]
        [Authorize(Policy = "accountmanagement.printslip")]
        public async Task<IActionResult> PrintDriverSlip(string Id)
        {
            var ids = Id.Split(",");
            var report = "";
            foreach (var item1 in ids)
            {
                if (!string.IsNullOrWhiteSpace(item1))
                {
                    var filepath = Path.Combine(_env.WebRootPath, "ReportTemplates", "DrivertPd", "DrivertPd.html");
                    var header = await _context.DriverPaymentHeader.FindAsync(Convert.ToInt64(item1));
                    if (header == null)
                        return NotFound();
                    var details = await _context.RptDriverPaySlip.Where(t => t.HeaderID == header.Id).ToListAsync();

                    DataTable table = new DataTable();
                    DataColumn dataColumn1 = new DataColumn("اسم المرسل");
                    table.Columns.Add(dataColumn1);

                    DataColumn dataColumn2 = new DataColumn($"Tracking Number{Environment.NewLine}رقم الشحنة");
                    table.Columns.Add(dataColumn2);


                    DataColumn dataColumn3 = new DataColumn($"Client Name{Environment.NewLine}اسم العميل");
                    table.Columns.Add(dataColumn3);

                    DataColumn dataColumn4 = new DataColumn($"Client City{Environment.NewLine}مدينة العميل");
                    table.Columns.Add(dataColumn4);

                    DataColumn dataColumn5 = new DataColumn($"Client Area{Environment.NewLine}منطقة العميل");
                    table.Columns.Add(dataColumn5);

                    DataColumn dataColumn6 = new DataColumn($"Date{Environment.NewLine}التاريخ");
                    table.Columns.Add(dataColumn6);

                    DataColumn dataColumn7 = new DataColumn($"Shipment Total{Environment.NewLine}المجموع الكلي");
                    table.Columns.Add(dataColumn7);

                    //DataColumn dataColumn8 = new DataColumn($"Shipment fees{Environment.NewLine}رسوم الشحن");
                    //table.Columns.Add(dataColumn8);

                    //DataColumn dataColumn9 = new DataColumn($"Extra fees{Environment.NewLine}رسوم إضافية");
                    //table.Columns.Add(dataColumn9);

                    DataColumn dataColumn8 = new DataColumn($"Driver Comision{Environment.NewLine}عمولة السائق");
                    table.Columns.Add(dataColumn8);

                    DataColumn dataColumn9 = new DataColumn($"Driver Extra Comision{Environment.NewLine}عمولة اضافية للسائق");
                    table.Columns.Add(dataColumn9);

                    foreach (var item in details)
                    {
                        DataRow dataRow = table.NewRow();
                        dataRow[0] = item?.SenderName;
                        dataRow[1] = item?.ShipmentTrackingNo;
                        dataRow[2] = item?.SenderName;
                        dataRow[3] = item?.CityName;
                        dataRow[4] = item?.AreaName;
                        dataRow[5] = item?.EntryDate.ToString("dd/MM/yyyy");
                        dataRow[6] = item?.ShipmentPrice;
                        //dataRow[6] = item.ShipmentFees;
                        //dataRow[7] = item.ShipmentExtraFees;
                        dataRow[7] = item?.ComisionValue;
                        dataRow[8] = item?.DriverExtraComisionValue;
                        table.Rows.Add(dataRow);


                    }
                    var drive = await _context.Drives.FindAsync(header.DriverId);
                    var user = await _context.Users.FindAsync(header.EntryUserId);
                    StreamReader sr = new StreamReader(filepath);
                    string s = sr.ReadToEnd();
                    s = s.Replace("{details}", ConvertToHtml(table));
                    s = s.Replace("{Date}", Convert.ToDateTime(header?.PaymentDate).ToString("dd/MM/yyyy"));
                    s = s.Replace("{Total delivery shipments}", details.Count().ToString());
                    s = s.Replace("{Total Amount}", header?.PaymentValue.ToString());
                    s = s.Replace("{Ref}", header?.Id.ToString());
                    s = s.Replace("{Total COD}", header?.TotalCod.ToString());
                    s = s.Replace("{Driver Name}", drive?.FirstName + " " + drive?.SecoundName + " " + drive?.FamilyName);
                    s = s.Replace("{Act User Name}", user?.FirstName + " " + user?.LastName);
                    sr.Close();
                    report += s;
                }
            }

            ViewBag.report = report;
            return View("PrintSlip");
        }

        [HttpGet]
        [Authorize(Policy = "accountmanagement.printslip")]
        public async Task<IActionResult> PrintUserSlip(string Id)
        {
            //var ids = Id.Split(",");
            //var report = "";
            //foreach (var item1 in ids)
            //{
            //    if (!string.IsNullOrWhiteSpace(item1))
            //    {
            //        var filepath = Path.Combine(_env.WebRootPath, "ReportTemplates", "CashDeliveryInvoice", "CashDeliveryInvoice.html");
            //        var header = await _context.BisnessUserPaymentHeaders.FindAsync(Convert.ToInt64(item1));
            //        if (header == null)
            //            return NotFound();
            //        var details = await _context.PayBusinessSlipViews.Where(t => t.Id == header.Id).ToListAsync();

            //        DataTable table = new DataTable();
            //        //DataColumn dataColumn1 = new DataColumn("ID");
            //        //table.Columns.Add(dataColumn1);
            //        DataColumn dataColumn1 = new DataColumn($"Client Name{Environment.NewLine}اسم العميل");
            //        table.Columns.Add(dataColumn1);

            //        DataColumn dataColumn2 = new DataColumn($"محتويات الطرد");
            //        table.Columns.Add(dataColumn2);

            //        DataColumn dataColumn3 = new DataColumn($"Total{Environment.NewLine}الاجمالي");
            //        table.Columns.Add(dataColumn3);

            //        DataColumn dataColumn4 = new DataColumn($"Shipment Price{Environment.NewLine}سعر الشحنة");
            //        table.Columns.Add(dataColumn4);

            //        DataColumn dataColumn5 = new DataColumn($"Shipment fees{Environment.NewLine}رسوم الشحن");
            //        table.Columns.Add(dataColumn5);

            //        DataColumn dataColumn6 = new DataColumn($"Tracking Number{Environment.NewLine}رقم الشحنة");
            //        table.Columns.Add(dataColumn6);

            //        DataColumn dataColumn7 = new DataColumn($"Date{Environment.NewLine}التاريخ");
            //        table.Columns.Add(dataColumn7);



            //        DataColumn dataColumn8 = new DataColumn($"Area{Environment.NewLine}منطقة العميل");
            //        table.Columns.Add(dataColumn8);


            //        DataColumn dataColumn9 = new DataColumn($"Extra fees{Environment.NewLine}رسوم إضافية");
            //        table.Columns.Add(dataColumn9);

            //        //DataColumn dataColumn5 = new DataColumn($"Client City{Environment.NewLine}مدينة العميل");
            //        //table.Columns.Add(dataColumn5);









            //        //DataColumn dataColumn10 = new DataColumn($"Return Fees{Environment.NewLine}رسوم الارجاع");
            //        //table.Columns.Add(dataColumn10); 





            //        foreach (var item in details)
            //        {
            //            DataRow dataRow = table.NewRow();
            //            //dataRow[0] = item?.Id;
            //            dataRow[5] = item?.ShipmentTrackingNo;
            //            if (item.ShipmentContains != null)
            //            {

            //                item.ShipmentContains = kkk(item.ShipmentContains);

            //            }

            //            dataRow[1] = $"<div style='word-wrap: break-word;'>{item?.ShipmentContains}</div>";

            //            if (item.ClientName != null)
            //            {


            //                item.ClientName = kkk(item.ClientName);

            //            }


            //            dataRow[0] = item?.ClientName;
            //            //dataRow[3] = item?.CityName;
            //            dataRow[7] = item?.AreaName;
            //            dataRow[6] = Convert.ToDateTime(item.EntryDate).ToString("yyyy-MM-dd");
            //            dataRow[3] = item?.ShipmentPrice;
            //            dataRow[4] = item?.ShipmentFees;
            //            dataRow[8] = item?.ShipmentExtraFees;
            //            //  dataRow[7] = item?.ReturnFees;
            //            dataRow[2] = item?.ShipmentTotal;
            //            table.Rows.Add(dataRow);


            //        }
            //        var buss = await _context.Users.FindAsync(header?.UserId);
            //        var user = await _context.Users.FindAsync(header?.LoginUserId);
            //        var t = await _context.CompanyBranches.FindAsync(user?.CompanyBranchId);
            //        StreamReader sr = new StreamReader(filepath);
            //        string s = sr.ReadToEnd();
            //        s = s.Replace("{details}", ConvertToHtml(table));
            //        s = s.Replace("{Date}", Convert.ToDateTime(header?.PaymentDate).ToString("yyyy-MM-dd"));
            //        s = s.Replace("{Total delivery shipments}", details.Count().ToString());
            //        s = s.Replace("{Total Amount}", header?.PaymentValue.ToString());
            //        s = s.Replace("{Ref}", header?.Id.ToString());
            //        s = s.Replace("{Return Fee}", details?.Sum(t => t?.ReturnFees).ToString());
            //        s = s.Replace("{Shipping expenses}", details?.Sum(t => t?.ShipmentFees).ToString());
            //        s = s.Replace("{Extra charge}", details?.Sum(t => t?.ShipmentExtraFees).ToString());
            //        s = s.Replace("{Customer dues}", header?.PaymentValue.ToString());
            //        s = s.Replace("{AgentName}", buss?.FirstName + " " + buss?.LastName);
            //        s = s.Replace("{Act User Name}", user?.FirstName + " " + user?.LastName);
            //        s = s.Replace("{address}", buss?.Address);
            //        s = s.Replace("{mobile}", buss?.MobileNo1);
            //        s = s.Replace("{area}", t.BranchName.ToString());
            //        sr.Close();
            //        report += s;
            //    }
            //}

            //ViewBag.report = report;
            return Redirect("/PrintUserSlip?Id=" + Id);
        }

        public IActionResult UrlDatasourceDriverPendingStatment([FromBody] DataManagerRequest dm, int DriverId = 111111111)
        {

            var DataSource = _context.Shipments.Where(r => r.DriverId == DriverId & r.Status != 8 & r.Status != 23 &
            r.Status != 10).AsQueryable();

            DataOperations operation = new DataOperations();
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);  //Search
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            if (dm.Where != null && dm.Where.Count > 0) //Filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
            }
            int count = DataSource.Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }
            return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);
        }




        [HttpGet]
        public async Task<IActionResult> PrintPDF(string Id)
        {
            var ids = Id.Split(",");
            var report = "";
            foreach (var item1 in ids)
            {
                if (!string.IsNullOrWhiteSpace(item1))
                {
                    var filepath = Path.Combine(_env.WebRootPath, "ReportTemplates", "CashDeliveryInvoice", "CashDeliveryInvoice.html");
                    var header = await _context.BisnessUserPaymentHeader.FindAsync(Convert.ToInt64(item1));
                    if (header == null)
                        return NotFound();
                    var details = await _context.PayBusinessSlipView.Where(t => t.Id == header.Id).ToListAsync();

                    DataTable table = new DataTable();
                    //DataColumn dataColumn1 = new DataColumn("ID");
                    //table.Columns.Add(dataColumn1);
                    DataColumn dataColumn1 = new DataColumn($"Client Name{Environment.NewLine}اسم العميل");
                    table.Columns.Add(dataColumn1);

                    DataColumn dataColumn2 = new DataColumn($"محتويات الطرد");
                    table.Columns.Add(dataColumn2);

                    DataColumn dataColumn3 = new DataColumn($"Total{Environment.NewLine}الاجمالي");
                    table.Columns.Add(dataColumn3);

                    DataColumn dataColumn4 = new DataColumn($"Shipment Price{Environment.NewLine}سعر الشحنة");
                    table.Columns.Add(dataColumn4);

                    DataColumn dataColumn5 = new DataColumn($"Shipment fees{Environment.NewLine}رسوم الشحن");
                    table.Columns.Add(dataColumn5);

                    DataColumn dataColumn6 = new DataColumn($"Tracking Number{Environment.NewLine}رقم الشحنة");
                    table.Columns.Add(dataColumn6);

                    DataColumn dataColumn7 = new DataColumn($"Date{Environment.NewLine}التاريخ");
                    table.Columns.Add(dataColumn7);



                    DataColumn dataColumn8 = new DataColumn($"Area{Environment.NewLine}منطقة العميل");
                    table.Columns.Add(dataColumn8);


                    DataColumn dataColumn9 = new DataColumn($"Extra fees{Environment.NewLine}رسوم إضافية");
                    table.Columns.Add(dataColumn9);

                    DataColumn dataColumn10 = new DataColumn($"BarCode");
                    table.Columns.Add(dataColumn10);


                    //DataColumn dataColumn5 = new DataColumn($"Client City{Environment.NewLine}مدينة العميل");
                    //table.Columns.Add(dataColumn5);









                    //DataColumn dataColumn10 = new DataColumn($"Return Fees{Environment.NewLine}رسوم الارجاع");
                    //table.Columns.Add(dataColumn10); 





                    foreach (var item in details)
                    {
                        DataRow dataRow = table.NewRow();
                        //dataRow[0] = item?.Id;
                        dataRow[5] = item?.ShipmentTrackingNo;
                        if (item.ShipmentContains != null)
                        {

                            item.ShipmentContains = kkk(item.ShipmentContains);

                        }

                        dataRow[1] = $"<div style='word-wrap: break-word;'>{item?.ShipmentContains}</div>";

                        if (item.ClientName != null)
                        {


                            item.ClientName = kkk(item.ClientName);

                        }


                        dataRow[0] = item?.ClientName;
                        //dataRow[3] = item?.CityName;
                        dataRow[7] = item?.AreaName;
                        dataRow[6] = Convert.ToDateTime(item.EntryDate).ToString("dd/MM/yyyy");
                        dataRow[3] = item?.ShipmentPrice;
                        dataRow[4] = item?.ShipmentFees;
                        dataRow[8] = item?.ShipmentExtraFees;
                        //  dataRow[7] = item?.ReturnFees;
                        dataRow[2] = item?.ShipmentTotal;
                        dataRow[9] = item?.ShipmentTrackingNo;
                        table.Rows.Add(dataRow);


                    }
                    var buss = await _context.Users.FindAsync(header?.UserId);
                    var user = await _context.Users.FindAsync(header?.LoginUserId);
                    var t = await _context.CompanyBranches.FindAsync(user?.CompanyBranchId);
                    StreamReader sr = new StreamReader(filepath);
                    string s = sr.ReadToEnd();
                    s = s.Replace("{details}", ConvertToHtml2(table));

                    s = s.Replace("{Date}", Convert.ToDateTime(header?.PaymentDate).ToString("dd/MM/yyyy"));
                    s = s.Replace("{Total delivery shipments}", details.Count().ToString());
                    s = s.Replace("{Total Amount}", header?.PaymentValue.ToString());
                    s = s.Replace("{Ref}", header?.Id.ToString());
                    s = s.Replace("{Return Fee}", details?.Sum(t => t?.ReturnFees).ToString());
                    s = s.Replace("{Shipping expenses}", details?.Sum(t => t?.ShipmentFees).ToString());
                    s = s.Replace("{Extra charge}", details?.Sum(t => t?.ShipmentExtraFees).ToString());
                    s = s.Replace("{Customer dues}", header?.PaymentValue.ToString());
                    s = s.Replace("{AgentName}", buss?.FirstName + " " + buss?.LastName);
                    s = s.Replace("{Act User Name}", user?.FirstName + " " + user?.LastName);
                    s = s.Replace("{address}", buss?.Address);
                    s = s.Replace("{mobile}", buss?.MobileNo1);
                    s = s.Replace("{area}", t.BranchName.ToString());
                    sr.Close();
                    report += s;
                }
            }

            ViewBag.report = report;
            return View("PrintSlip");
        }
        public string kkk(string sentence)
        {
            int myLimit = 8;
            string[] words = sentence.Split(' ');

            StringBuilder newSentence = new StringBuilder();


            string line = "";
            foreach (string word in words)
            {
                if ((line + word).Length > myLimit)
                {
                    newSentence.AppendLine(line);
                    line = "";
                }

                line += string.Format("{0} ", word);
            }

            if (line.Length > 0)
                newSentence.AppendLine(line);

            return newSentence.ToString();
        }
        private string ConvertToHtml(DataTable dt)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            //sb.AppendLine("<html>");
            //sb.AppendLine("<body>");
            sb.AppendLine("<table  width='90%' id='report' BORDER ='1'>");
            foreach (DataColumn dc in dt.Columns)
            {
                sb.AppendFormat("<th align = 'center'>{0}</th>", dc.ColumnName);
            }

            foreach (DataRow dr in dt.Rows)

            {
                sb.Append("<tr>");
                foreach (DataColumn dc in dt.Columns)
                {
                    string cellValue = dr[dc] != null ? dr[dc].ToString() : "";
                    sb.AppendFormat("<td>{0}</ td>", cellValue);
                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
            //sb.AppendLine("</body>");
            //sb.AppendLine("</html>");
            return sb.ToString();
        }

        private string ConvertToHtml2(DataTable dt)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            //sb.AppendLine("<html>");
            //sb.AppendLine("<body>");
            sb.AppendLine("<table id='report' BORDER ='1'>");
            foreach (DataColumn dc in dt.Columns)
            {
                if (!dc.ColumnName.Contains("Tracking Number"))
                {
                    sb.AppendFormat("<th align = 'center'>{0}</th>", dc.ColumnName);

                }
            }

            foreach (DataRow dr in dt.Rows)
            {

                sb.Append("<tr>");
                foreach (DataColumn dc in dt.Columns)
                {
                    if (!dc.ColumnName.Contains("Tracking Number"))
                    {
                        if (dc.ColumnName == "BarCode")
                        {
                            string cellValue = dr[dc] != null ? dr[dc].ToString() : "";
                            sb.Append("<td><svg id='" + cellValue + "'></svg>  <script> JsBarcode('#" + cellValue + "', '" + cellValue + "', {  width: 2, height: 30,  displayValue: true });  </script>  </td>");
                        }
                        else
                        {
                            string cellValue = dr[dc] != null ? dr[dc].ToString() : "";
                            sb.AppendFormat("<td>{0}</ td>", cellValue);
                        }
                    }


                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
            //sb.AppendLine("</body>");
            //sb.AppendLine("</html>");
            return sb.ToString();
        }

    }

    public class Data
    {
        public int take { get; set; }
        public List<Wheres> where { get; set; }
    }
    public class Wheres
    {
        public string field { get; set; }
        public bool ignoreAccent { get; set; }

        public bool ignoreCase { get; set; }

        public bool isComplex { get; set; }

        public string value { get; set; }
        public string Operator { get; set; }

    }

    public class ICRUDModel<T> where T : class
    {
        public string action { get; set; }

        public string table { get; set; }

        public string keyColumn { get; set; }

        public object key { get; set; }

        public T value { get; set; }

        public List<T> added { get; set; }

        public List<T> changed { get; set; }

        public List<T> deleted { get; set; }

        public IDictionary<string, object> @params { get; set; }
    }

    public enum StatusEnum
    {
        Draft = 1,
        Submitted = 2,
        ReturnPendingPickup = 3,
        PickedUpOffice = 4,
        ONHOLD = 5,
        Returned = 7,
        CODPickup = 8,
        InAccounting = 9,
        Closed = 10,
        Cancelled = 11,
        TransferBranch = 12,
        Readyforpickup = 13,
        WithDriver = 14,
        ReturnExchange = 15,
        ExchangePickedUP = 16,
        Transferofficereturn = 17,
        ClosedReturned = 23
    }
}