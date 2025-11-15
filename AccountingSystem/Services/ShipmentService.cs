using AccountingSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roadfn.Models;
using Roadfn.ViewModel;
using System.Data;
using System.Linq;

namespace Roadfn.Services
{

    public record ShipmentFeeLookupResult(decimal DeliveryFee, decimal? ReturnFee);


    public interface IShipmentService
    {
        public Task<ShipmentsTrackingGeneratedCode> GetShipmentsTrackingGeneratedCode(int UserId);
        public Task<int> GcreateNewShipmentsConfirm(CreateShipmentViewModel createShipmentViewModel, int userId);
        public Task<int> GcreateNewShipmentsConfirm(BussCreateShipmentViewModel createShipmentViewModel, int userId, int MarketerId);
        public Task<int> GcreateNewShipmentsConfirm(BussCreateShipmentExcelViewModel createShipmentViewModel, int userId, int MarketerId);
        public Task GcreateNewShipmentsDraft(CreateShipmentViewModel createShipmentViewModel, int userId);
        public Task GcreateNewShipmentsDraft(BussCreateShipmentViewModel createShipmentViewModel, int userId, int MarketerId);
        public Task BusinessClient(Shipment shipment);
        public Task<ShipmentFeeLookupResult> ResolveBusinessShipmentFeesAsync(int businessUserId, int cityId, int? areaId);
    }
    public class ShipmentService : IShipmentService
    {
        private RoadFnDbContext _context;
        private readonly IConfiguration iConfig;
        public ShipmentService(RoadFnDbContext context, IConfiguration iConfig)
        {
            _context = context;
            this.iConfig = iConfig;

        }

        public async Task<ShipmentFeeLookupResult> ResolveBusinessShipmentFeesAsync(int businessUserId, int cityId, int? areaId)
        {
            ShipmentFee? userAreaFee = null;
            if (areaId.HasValue)
            {
                userAreaFee = await _context.ShipmentFees
                    .Where(f => f.UserBusinessId == businessUserId
                                && f.ToCityId == cityId
                                && f.ToAreaId == areaId
                                && f.IsBusiness == true)
                    .FirstOrDefaultAsync();

                if (userAreaFee?.Fees != null)
                {
                    return new ShipmentFeeLookupResult(userAreaFee.Fees.Value, userAreaFee.ReturnFees);
                }

                var areaGeneralFee = await _context.AreaGeneralFees
                    .Where(f => f.AreaId == areaId.Value && (!f.CityId.HasValue || f.CityId == cityId))
                    .OrderByDescending(f => f.CityId.HasValue)
                    .FirstOrDefaultAsync();

                if (areaGeneralFee?.Fees != null)
                {
                    return new ShipmentFeeLookupResult(areaGeneralFee.Fees.Value, areaGeneralFee.ReturnFees);
                }
            }

            var userCityFee = await _context.ShipmentFees
                .Where(f => f.UserBusinessId == businessUserId
                            && f.ToCityId == cityId
                            && f.IsBusiness == true
                            && f.ToAreaId == null)
                .FirstOrDefaultAsync();

            if (userCityFee?.Fees != null)
            {
                return new ShipmentFeeLookupResult(userCityFee.Fees.Value, userCityFee.ReturnFees);
            }

            throw new InvalidOperationException("تعرفة الشحن غير موجودة لهذه المدينة.");
        }

        public async Task BusinessClient(Shipment shipment)
        {
            var buss = await _context.BusinessClient.Where(t => t.Mobile1 == shipment.ClientPhone && t.BisnessUserId == shipment.BusinessUserId).FirstOrDefaultAsync();
            if (buss == null)
            {
                buss = new BusinessClient();
                buss.Area = shipment.ClientAreaId;
                buss.City = shipment.ClientCityId;
                buss.BisnessUserId = shipment.BusinessUserId;
                buss.Address = shipment?.ClientAddress;
                buss.Mobile1 = shipment?.ClientPhone;
                buss.Mobile2 = shipment?.ClientPhone2;
                buss.Name = shipment?.ClientName;
                await _context.BusinessClient.AddAsync(buss);

            }
            else
            {
                buss.Area = shipment.ClientAreaId;
                buss.City = shipment.ClientCityId;
                buss.BisnessUserId = shipment.BusinessUserId;
                buss.Address = shipment.ClientAddress;
                buss.Mobile1 = shipment.ClientPhone;
                buss.Mobile2 = shipment.ClientPhone2;
                buss.Name = shipment.ClientName;
                _context.BusinessClient.Update(buss);
            }
            await _context.SaveChangesAsync();
        }

        public async Task<int> GcreateNewShipmentsConfirm(CreateShipmentViewModel createShipmentViewModel, int userId)
        {
            var shipId = 0;
            var user = await _context.Users.FindAsync(createShipmentViewModel.BusinessUserID);
            Shipment shipment = new Shipment();
            shipment.Alert = createShipmentViewModel.Alert;



            shipment.IsForeign = createShipmentViewModel.IsForeign;
            shipment.ShipmentQuantity = createShipmentViewModel.ShipmentQuantity;
            shipment.BusinessUserId = user.Id;
            shipment.ClientName = createShipmentViewModel.ClientName;
            shipment.ShipmentTypeId = createShipmentViewModel.ShipmentTypeID;
            shipment.ClientLandLine = createShipmentViewModel.ClientAddress;
            shipment.ClientAddress = createShipmentViewModel.ClientAddress;
            shipment.ClientAreaId = Convert.ToInt32(createShipmentViewModel.ClientAreaID);
            shipment.ClientCityId = Convert.ToInt32(createShipmentViewModel.ClientCityID);
            shipment.ClientPhone = createShipmentViewModel.ClientPhone;
            shipment.ClientPhone2 = createShipmentViewModel.ClientPhone2;
            shipment.FromCityId = user.CityId;
            shipment.ShipmentFeesDiscount = Convert.ToDecimal(createShipmentViewModel.ShipmentFeesDiscount);
            shipment.ShipmentContains = createShipmentViewModel.ShipmentContains;
            shipment.EntryDateTime = DateTime.Now;
            shipment.EntryDate = DateTime.Now;
            shipment.IsReturn = createShipmentViewModel.IsReturn;
            shipment.DriverCanOpenShipment = createShipmentViewModel.DriverCanOpenShipment;
            shipment.Remarks = createShipmentViewModel.Remarks;
            shipment.LastUpdate = DateTime.Now;
            shipment.SenderName = $"{user.FirstName} {user.LastName}";
            shipment.SenderTel = user.MobileNo1;
            shipment.Status = (int?)StatusEnum.Submitted;
            shipment.ShipmentTrackingNo = createShipmentViewModel.ShipmentTrackingNo;
            shipment.ShipmentPrice = Convert.ToDecimal(createShipmentViewModel.ShipmentPrice);

            shipment.PaidAmountFromShipmentFees = Convert.ToDecimal(createShipmentViewModel.PaidAmountFromShipmentFees);
            shipment.ShipmentExtraFees = Convert.ToDecimal(createShipmentViewModel.ShipmentExtraFees);
            shipment.ShipmentTotal = Convert.ToDecimal(createShipmentViewModel.ShipmentTotal);
            shipment.RetPay = createShipmentViewModel.RetPay;
            shipment.rangeVal = createShipmentViewModel.rangeVal;
            shipment.ShipmentFees = Convert.ToDecimal(createShipmentViewModel.ShipmentFees);

            var totdiscopunt = Convert.ToDecimal(shipment.PaidAmountFromShipmentFees) + Convert.ToDecimal(shipment.ShipmentFeesDiscount);

            if (totdiscopunt > shipment.ShipmentFees)
            {
                shipment.ShipmentFees = 0;
            }
            else
            {
                shipment.ShipmentFees -= totdiscopunt;
            }

            // shipment.ShipmentPrice = shipment.ShipmentTotal - shipment.ShipmentExtraFees - shipment.ShipmentFees;
            shipment.ShipmentTotal = Convert.ToDecimal(shipment.ShipmentPrice) + Convert.ToDecimal(shipment.ShipmentExtraFees) + Convert.ToDecimal(shipment.ShipmentFees);
            shipment.UserId = userId;
            shipment.OldShipmentPrice = shipment.ShipmentPrice;
            await _context.Shipments.AddAsync(shipment);
            await _context.SaveChangesAsync();
            var adduser = await _context.Users.FindAsync(userId);

            var bb = 0;
            if (Convert.ToDecimal(shipment.PaidAmountFromShipmentFees) > 0)
            {


            }




            await BusinessClient(shipment);
            shipId = shipment.Id;
            //add Submitted status
            ShipmentLog shipmentLog = new ShipmentLog();
            shipmentLog.ShipmentId = shipment.Id;
            shipmentLog.EntryDate = DateTime.Now;
            shipmentLog.EntryDateTine = DateTime.Now;
            shipmentLog.UserId = shipment.UserId;
            shipmentLog.Status = shipment.Status;
            shipmentLog.ClientName = shipment.ClientName;
            shipmentLog.ClientPhone = shipment.ClientPhone;
            shipmentLog.FromCityId = shipment.FromCityId;
            shipmentLog.ClientCityId = shipment.ClientCityId;
            shipmentLog.ClientAreaId = shipment.ClientAreaId;
            shipmentLog.IsUserBusiness = shipment.IsUserBusiness;
            shipmentLog.SenderName = shipment.SenderName;
            shipmentLog.SenderTel = shipment.SenderTel;
            shipmentLog.BusinessUserId = shipment.BusinessUserId;
            //shipmentLog.BranchId = user.CompanyBranchId;
            shipmentLog.Status = (int?)StatusEnum.Draft;
            await _context.ShipmentLogs.AddAsync(shipmentLog);
            await _context.SaveChangesAsync();

            //add draft log
            ShipmentLog shipment1 = new ShipmentLog();
            shipment1 = shipmentLog;
            shipment1.Id = 0;
            shipment1.Status = (int?)StatusEnum.Submitted;
            await _context.ShipmentLogs.AddAsync(shipment1);
            await _context.SaveChangesAsync();

            SessionAddRemark sessionAddRemark = new SessionAddRemark();
            sessionAddRemark.ShipmentId = shipment.Id;
            sessionAddRemark.UserId = shipment.UserId;
            sessionAddRemark.BranchId = shipment.UserId;
            sessionAddRemark.EntryDateTime = DateTime.Now;
            sessionAddRemark.OldStatus = (int?)StatusEnum.Draft;
            sessionAddRemark.NewStatus = (int?)StatusEnum.Submitted;
            sessionAddRemark.Remark = shipment.Alert;
            sessionAddRemark.EntryDate = DateTime.Now;
            //sessionAddRemark.BranchId = user.CompanyBranchId;
            await _context.Session_Add_Remarks.AddAsync(sessionAddRemark);
            await _context.SaveChangesAsync();
            //add  Return shipment
            if (createShipmentViewModel.IsReturn)
            {
                var ex = await _context.ShipmentStatuses.Where(t => t.ExStart == true).FirstOrDefaultAsync();

                Shipment shipmentRet = new Shipment();
                shipmentRet = shipment;
                shipmentRet.IsForeign = createShipmentViewModel.IsForeign;
                shipmentRet.ShipmentQuantity = createShipmentViewModel.ShipmentQuantity;
                shipmentRet.Id = 0;
                shipmentRet.ShipmentTrackingNo = shipment.ShipmentTrackingNo + "_ex";
                shipmentRet.IsReturn = false;
                shipmentRet.ShipmentFees = 0;
                shipmentRet.Status = ex.Id;
                shipmentRet.ShipmentExtraFees = 0;
                shipmentRet.ShipmentTotal = 0;
                shipmentRet.ShipmentPrice = 0;
                await _context.Shipments.AddAsync(shipmentRet);
                await _context.SaveChangesAsync();
                //add Submitted status
                ShipmentLog shipmentLogwx = new ShipmentLog();
                shipmentLogwx.ShipmentId = shipmentRet.Id;
                shipmentLogwx.EntryDate = DateTime.Now;
                shipmentLogwx.EntryDateTine = DateTime.Now;
                shipmentLogwx.UserId = shipmentRet.UserId;
                shipmentLogwx.Status = ex.Id;
                shipmentLogwx.ClientName = shipmentRet.ClientName;
                shipmentLogwx.ClientPhone = shipmentRet.ClientPhone;
                shipmentLogwx.ClientCityId = shipmentRet.ClientCityId;
                shipmentLogwx.ClientAreaId = shipmentRet.ClientAreaId;
                shipmentLogwx.FromCityId = shipmentRet.FromCityId;
                shipmentLogwx.IsUserBusiness = shipmentRet.IsUserBusiness;
                shipmentLogwx.SenderName = shipmentRet.SenderName;
                shipmentLogwx.SenderTel = shipmentRet.SenderTel;
                shipmentLogwx.BusinessUserId = shipmentRet.BusinessUserId;
                await _context.ShipmentLogs.AddAsync(shipmentLogwx);
                await _context.SaveChangesAsync();

                //add draft log
                ShipmentLog shipment2log = new ShipmentLog();
                shipment2log = shipmentLogwx;
                shipment2log.Id = 0;
                shipment2log.Status = ex.Id;
                await _context.ShipmentLogs.AddAsync(shipment2log);
                await _context.SaveChangesAsync();

                SessionAddRemark sessionAddRemark1 = new SessionAddRemark();
                sessionAddRemark1.ShipmentId = shipmentLogwx.Id;
                sessionAddRemark1.UserId = shipment.UserId;
                //sessionAddRemark1.BranchId = shipment.UserId;
                sessionAddRemark1.EntryDateTime = DateTime.Now;
                sessionAddRemark1.NewStatus = ex.Id;
                sessionAddRemark1.Remark = shipment.Alert;
                sessionAddRemark1.EntryDate = DateTime.Now;
                sessionAddRemark1.BranchId = user.CompanyBranchId;
                await _context.Session_Add_Remarks.AddAsync(sessionAddRemark1);
                await _context.SaveChangesAsync();

            }
            return shipId;
        }


        public async Task<int> GcreateNewShipmentsConfirm(BussCreateShipmentViewModel createShipmentViewModel, int userId, int MarketerId)
        {
            var shipId = 0;
            var user = await _context.Users.FindAsync(createShipmentViewModel.BusinessUserID);
            Shipment shipment = new Shipment();
            shipment.MarketerId = MarketerId;
            shipment.IsForeign = createShipmentViewModel.IsForeign;
            shipment.ShipmentQuantity = createShipmentViewModel.ShipmentQuantity;
            shipment.Alert = createShipmentViewModel.Alert;
            shipment.BusinessUserId = createShipmentViewModel.BusinessUserID;
            shipment.ClientName = createShipmentViewModel.ClientName;
            shipment.lang = createShipmentViewModel.lang;
            shipment.ShipmentTypeId = createShipmentViewModel.ShipmentTypeID;
            shipment.ClientLandLine = createShipmentViewModel.ClientAddress;
            shipment.ClientAddress = createShipmentViewModel.ClientAddress;
            shipment.ClientAreaId = Convert.ToInt32(createShipmentViewModel.ClientAreaID);
            shipment.ClientCityId = Convert.ToInt32(createShipmentViewModel.ClientCityID);
            shipment.ClientPhone = createShipmentViewModel.ClientPhone;
            shipment.ClientPhone2 = createShipmentViewModel.ClientPhone2;
            shipment.FromCityId = user.CityId;
            shipment.ShipmentContains = createShipmentViewModel.ShipmentContains;
            shipment.EntryDateTime = DateTime.Now;
            shipment.EntryDate = DateTime.Now;
            shipment.IsReturn = createShipmentViewModel.IsReturn;
            shipment.Remarks = createShipmentViewModel.Remarks;
            shipment.LastUpdate = DateTime.Now;
            shipment.SenderName = $"{user.FirstName} {user.LastName}";
            shipment.SenderTel = user.MobileNo1;
            shipment.Status = (int?)StatusEnum.Submitted;
            shipment.ShipmentTrackingNo = createShipmentViewModel.ShipmentTrackingNo;
            shipment.ShipmentFees = createShipmentViewModel.ShipmentFees;
            shipment.ClientChatUrl = createShipmentViewModel.ClientChatUrl;
            shipment.ClientMapAddress = createShipmentViewModel.ClientMapAddress;
            shipment.DriverCanOpenShipment = createShipmentViewModel.DriverCanOpenShipment;

            //shipment.ShipmentExtraFees = createShipmentViewModel.ShipmentExtraFees;
            shipment.ShipmentTotal = createShipmentViewModel.ShipmentTotal;
            shipment.ShipmentPrice = createShipmentViewModel.ShipmentPrice;
            shipment.rangeVal = createShipmentViewModel.rangeVal;
            shipment.RetPay = createShipmentViewModel.RetPay;
            shipment.UserId = userId;
            shipment.rangeVal = 1;
            shipment.BranchId = user.CompanyBranchId;
            shipment.OldShipmentPrice = shipment.ShipmentPrice;
            await _context.Shipments.AddAsync(shipment);
            await _context.SaveChangesAsync();
            await BusinessClient(shipment);
            shipId = shipment.Id; ;
            //add Submitted status
            ShipmentLog shipmentLog = new ShipmentLog();
            shipmentLog.ShipmentId = shipment.Id;
            shipmentLog.EntryDate = DateTime.Now;
            shipmentLog.EntryDateTine = DateTime.Now;
            shipmentLog.UserId = shipment.UserId;
            shipmentLog.Status = shipment.Status;
            shipmentLog.ClientName = shipment.ClientName;
            shipmentLog.ClientPhone = shipment.ClientPhone;
            shipmentLog.FromCityId = shipment.FromCityId;
            shipmentLog.ClientCityId = shipment.ClientCityId;
            shipmentLog.ClientAreaId = shipment.ClientAreaId;
            shipmentLog.IsUserBusiness = shipment.IsUserBusiness;
            shipmentLog.SenderName = shipment.SenderName;
            shipmentLog.SenderTel = shipment.SenderTel;
            shipmentLog.BusinessUserId = shipment.BusinessUserId;
            //shipmentLog.BranchId = user.CompanyBranchId;
            shipmentLog.Status = (int?)StatusEnum.Draft;
            await _context.ShipmentLogs.AddAsync(shipmentLog);
            await _context.SaveChangesAsync();

            //add draft log
            ShipmentLog shipment1 = new ShipmentLog();
            shipment1 = shipmentLog;
            shipment1.Id = 0;
            shipment1.Status = (int?)StatusEnum.Submitted;
            await _context.ShipmentLogs.AddAsync(shipment1);
            await _context.SaveChangesAsync();

            SessionAddRemark sessionAddRemark = new SessionAddRemark();
            sessionAddRemark.ShipmentId = shipment.Id;
            sessionAddRemark.UserId = shipment.UserId;
            sessionAddRemark.BranchId = shipment.UserId;
            sessionAddRemark.EntryDateTime = DateTime.Now;
            sessionAddRemark.OldStatus = (int?)StatusEnum.Draft;
            sessionAddRemark.NewStatus = (int?)StatusEnum.Submitted;
            sessionAddRemark.Remark = shipment.Alert;
            sessionAddRemark.EntryDate = DateTime.Now;
            //sessionAddRemark.BranchId = user.CompanyBranchId;
            await _context.Session_Add_Remarks.AddAsync(sessionAddRemark);
            await _context.SaveChangesAsync();
            //add  Return shipment
            if (createShipmentViewModel.IsReturn)
            {
                var ex = await _context.ShipmentStatuses.Where(t => t.ExStart == true).FirstOrDefaultAsync();

                Shipment shipmentRet = new Shipment();
                shipmentRet = shipment;
                shipmentRet.IsForeign = createShipmentViewModel.IsForeign;
                shipmentRet.ShipmentQuantity = createShipmentViewModel.ShipmentQuantity;
                shipmentRet.Id = 0;
                shipmentRet.ShipmentTrackingNo = shipment.ShipmentTrackingNo + "_ex";
                shipmentRet.IsReturn = false;
                shipmentRet.ShipmentFees = 0;
                shipmentRet.Status = ex.Id;
                shipmentRet.ShipmentExtraFees = 0;
                shipmentRet.ShipmentTotal = 0;
                shipmentRet.ShipmentPrice = 0;
                await _context.Shipments.AddAsync(shipmentRet);
                await _context.SaveChangesAsync();
                //add Submitted status
                ShipmentLog shipmentLogwx = new ShipmentLog();
                shipmentLogwx.ShipmentId = shipmentRet.Id;
                shipmentLogwx.EntryDate = DateTime.Now;
                shipmentLogwx.EntryDateTine = DateTime.Now;
                shipmentLogwx.UserId = shipmentRet.UserId;
                shipmentLogwx.Status = ex.Id;
                shipmentLogwx.ClientName = shipmentRet.ClientName;
                shipmentLogwx.ClientPhone = shipmentRet.ClientPhone;
                shipmentLogwx.ClientCityId = shipmentRet.ClientCityId;
                shipmentLogwx.ClientAreaId = shipmentRet.ClientAreaId;
                shipmentLogwx.FromCityId = shipmentRet.FromCityId;
                shipmentLogwx.IsUserBusiness = shipmentRet.IsUserBusiness;
                shipmentLogwx.SenderName = shipmentRet.SenderName;
                shipmentLogwx.SenderTel = shipmentRet.SenderTel;
                shipmentLogwx.BusinessUserId = shipmentRet.BusinessUserId;
                await _context.ShipmentLogs.AddAsync(shipmentLogwx);
                await _context.SaveChangesAsync();

                //add draft log
                ShipmentLog shipment2log = new ShipmentLog();
                shipment2log = shipmentLogwx;
                shipment2log.Id = 0;
                shipment2log.Status = ex.Id;
                await _context.ShipmentLogs.AddAsync(shipment2log);
                await _context.SaveChangesAsync();

                SessionAddRemark sessionAddRemark1 = new SessionAddRemark();
                sessionAddRemark1.ShipmentId = shipmentLogwx.Id;
                sessionAddRemark1.UserId = shipment.UserId;
                //sessionAddRemark1.BranchId = shipment.UserId;
                sessionAddRemark1.EntryDateTime = DateTime.Now;
                sessionAddRemark1.NewStatus = ex.Id;
                sessionAddRemark1.Remark = shipment.Alert;
                sessionAddRemark1.EntryDate = DateTime.Now;
                sessionAddRemark1.BranchId = user.CompanyBranchId;
                await _context.Session_Add_Remarks.AddAsync(sessionAddRemark1);
                await _context.SaveChangesAsync();

            }
            return shipId;
        }
        public async Task<int> GcreateNewShipmentsConfirm(BussCreateShipmentExcelViewModel createShipmentViewModel, int userId, int MarketerId)
        {
            var shipId = 0;
            var user = await _context.Users.FindAsync(createShipmentViewModel.BusinessUserID);
            Shipment shipment = new Shipment();
            shipment.MarketerId = MarketerId;
            shipment.IsForeign = false;
            shipment.ShipmentQuantity = 1;
            shipment.Alert = "";
            shipment.BusinessUserId = createShipmentViewModel.BusinessUserID;
            shipment.ClientName = createShipmentViewModel.ClientName;
            shipment.lang = "";
            shipment.ShipmentTypeId = 0;
            shipment.ClientLandLine = createShipmentViewModel.ClientAddress;
            shipment.ClientAddress = createShipmentViewModel.ClientAddress;
            shipment.ClientAreaId = Convert.ToInt32(createShipmentViewModel.ClientAreaID);
            shipment.ClientCityId = Convert.ToInt32(createShipmentViewModel.ClientCityID);
            shipment.ClientPhone = createShipmentViewModel.ClientPhone;
            shipment.ClientPhone2 = "";
            shipment.FromCityId = user.CityId;
            shipment.ShipmentContains = createShipmentViewModel.ShipmentContains;
            shipment.EntryDateTime = DateTime.Now;
            shipment.EntryDate = DateTime.Now;
            shipment.IsReturn = false;
            shipment.Remarks = "";
            shipment.LastUpdate = DateTime.Now;
            shipment.SenderName = $"{user.FirstName} {user.LastName}";
            shipment.SenderTel = user.MobileNo1;
            shipment.Status = (int?)StatusEnum.Submitted;
            shipment.ShipmentTrackingNo = createShipmentViewModel.ShipmentTrackingNo;
            shipment.ShipmentFees = createShipmentViewModel.ShipmentFees;
            shipment.ClientChatUrl = "";
            shipment.ClientMapAddress = "";
            shipment.DriverCanOpenShipment = true;

            //shipment.ShipmentExtraFees = createShipmentViewModel.ShipmentExtraFees;
            shipment.ShipmentTotal = createShipmentViewModel.ShipmentTotal;
            shipment.ShipmentPrice = createShipmentViewModel.ShipmentPrice;
            shipment.rangeVal = createShipmentViewModel.rangeVal;
            shipment.RetPay = false;
            shipment.UserId = userId;
            shipment.rangeVal = 1;
            shipment.BranchId = user.CompanyBranchId;
            shipment.OldShipmentPrice = shipment.ShipmentPrice;
            await _context.Shipments.AddAsync(shipment);
            await _context.SaveChangesAsync();
            await BusinessClient(shipment);
            shipId = shipment.Id; ;
            //add Submitted status
            ShipmentLog shipmentLog = new ShipmentLog();
            shipmentLog.ShipmentId = shipment.Id;
            shipmentLog.EntryDate = DateTime.Now;
            shipmentLog.EntryDateTine = DateTime.Now;
            shipmentLog.UserId = shipment.UserId;
            shipmentLog.Status = shipment.Status;
            shipmentLog.ClientName = shipment.ClientName;
            shipmentLog.ClientPhone = shipment.ClientPhone;
            shipmentLog.FromCityId = shipment.FromCityId;
            shipmentLog.ClientCityId = shipment.ClientCityId;
            shipmentLog.ClientAreaId = shipment.ClientAreaId;
            shipmentLog.IsUserBusiness = shipment.IsUserBusiness;
            shipmentLog.SenderName = shipment.SenderName;
            shipmentLog.SenderTel = shipment.SenderTel;
            shipmentLog.BusinessUserId = shipment.BusinessUserId;
            //shipmentLog.BranchId = user.CompanyBranchId;
            shipmentLog.Status = (int?)StatusEnum.Draft;
            await _context.ShipmentLogs.AddAsync(shipmentLog);
            await _context.SaveChangesAsync();

            //add draft log
            ShipmentLog shipment1 = new ShipmentLog();
            shipment1 = shipmentLog;
            shipment1.Id = 0;
            shipment1.Status = (int?)StatusEnum.Submitted;
            await _context.ShipmentLogs.AddAsync(shipment1);
            await _context.SaveChangesAsync();

            SessionAddRemark sessionAddRemark = new SessionAddRemark();
            sessionAddRemark.ShipmentId = shipment.Id;
            sessionAddRemark.UserId = shipment.UserId;
            sessionAddRemark.BranchId = shipment.UserId;
            sessionAddRemark.EntryDateTime = DateTime.Now;
            sessionAddRemark.OldStatus = (int?)StatusEnum.Draft;
            sessionAddRemark.NewStatus = (int?)StatusEnum.Submitted;
            sessionAddRemark.Remark = shipment.Alert;
            sessionAddRemark.EntryDate = DateTime.Now;
            //sessionAddRemark.BranchId = user.CompanyBranchId;
            await _context.Session_Add_Remarks.AddAsync(sessionAddRemark);
            await _context.SaveChangesAsync();
            //add  Return shipment
            return shipId;
        }

        public async Task GcreateNewShipmentsDraft(CreateShipmentViewModel createShipmentViewModel, int userId)
        {
            var iuser = await _context.Users.FirstOrDefaultAsync(t => t.Id == createShipmentViewModel.BusinessUserID);
            var user = await _context.Users.FindAsync(createShipmentViewModel.BusinessUserID);
            Shipment shipment = new Shipment();

            shipment.IsForeign = createShipmentViewModel.IsForeign;
            shipment.ShipmentQuantity = createShipmentViewModel.ShipmentQuantity;
            shipment.Alert = createShipmentViewModel.Alert;
            shipment.BusinessUserId = user.Id;
            shipment.ClientName = createShipmentViewModel.ClientName;
            shipment.ClientAddress = createShipmentViewModel.ClientAddress;
            shipment.ShipmentTypeId = createShipmentViewModel.ShipmentTypeID;
            shipment.ClientLandLine = createShipmentViewModel.ClientAddress;
            shipment.ClientAreaId = Convert.ToInt32(createShipmentViewModel.ClientAreaID);
            shipment.ClientCityId = Convert.ToInt32(createShipmentViewModel.ClientCityID);
            shipment.ClientPhone = createShipmentViewModel.ClientPhone;
            shipment.ClientPhone2 = createShipmentViewModel.ClientPhone2;
            shipment.FromCityId = user.CityId;
            shipment.ShipmentFeesDiscount = Convert.ToDecimal(createShipmentViewModel.ShipmentFeesDiscount);
            shipment.ShipmentContains = createShipmentViewModel.ShipmentContains;
            shipment.EntryDateTime = DateTime.Now;
            shipment.EntryDate = DateTime.Now;
            shipment.IsReturn = createShipmentViewModel.IsReturn;
            shipment.Remarks = createShipmentViewModel.Remarks;
            shipment.LastUpdate = DateTime.Now;
            shipment.SenderName = $"{user.FirstName} {user.LastName}";
            shipment.SenderTel = user.MobileNo1;
            shipment.Status = (int?)StatusEnum.Draft;
            shipment.ShipmentTrackingNo = createShipmentViewModel.ShipmentTrackingNo;
            shipment.ShipmentFees = createShipmentViewModel.ShipmentFees;
            shipment.PaidAmountFromShipmentFees = Convert.ToDecimal(createShipmentViewModel.PaidAmountFromShipmentFees);
            shipment.ShipmentExtraFees = Convert.ToDecimal(createShipmentViewModel.ShipmentExtraFees);
            shipment.ShipmentTotal = Convert.ToDecimal(createShipmentViewModel.ShipmentTotal);
            shipment.ShipmentPrice = Convert.ToDecimal(createShipmentViewModel.ShipmentPrice);
            shipment.UserId = userId;
            shipment.rangeVal = createShipmentViewModel.rangeVal;
            shipment.RetPay = createShipmentViewModel.RetPay;
            shipment.DriverCanOpenShipment = createShipmentViewModel.DriverCanOpenShipment;

            var totdiscopunt = shipment.PaidAmountFromShipmentFees + shipment.ShipmentFeesDiscount;

            if (totdiscopunt > shipment.ShipmentFees)
            {
                shipment.ShipmentFees = 0;
            }
            else
            {
                shipment.ShipmentFees -= totdiscopunt;
            }

            // shipment.ShipmentPrice = shipment.ShipmentTotal - shipment.ShipmentExtraFees - shipment.ShipmentFees;
            shipment.ShipmentTotal = Convert.ToDecimal(shipment.ShipmentPrice) + Convert.ToDecimal(shipment.ShipmentExtraFees) + shipment.ShipmentFees;
            shipment.OldShipmentPrice = shipment.ShipmentPrice;
            await _context.Shipments.AddAsync(shipment);
            await _context.SaveChangesAsync();
            var adduser = await _context.Users.FindAsync(userId);

            if (Convert.ToDecimal(shipment.PaidAmountFromShipmentFees) > 0)
            {

            }


            await BusinessClient(shipment);
            ShipmentLog shipmentLog = new ShipmentLog();
            shipmentLog.ShipmentId = shipment.Id;
            shipmentLog.EntryDate = DateTime.Now;
            shipmentLog.EntryDateTine = DateTime.Now;
            shipmentLog.UserId = shipment.UserId;
            shipmentLog.Status = shipment.Status;
            shipmentLog.ClientName = shipment.ClientName;
            shipmentLog.ClientPhone = shipment.ClientPhone;
            shipmentLog.ClientAreaId = shipment.ClientAreaId;
            shipmentLog.ClientCityId = shipment.ClientCityId;
            shipmentLog.IsUserBusiness = shipment.IsUserBusiness;
            shipmentLog.SenderName = shipment.SenderName;
            shipmentLog.SenderTel = shipment.SenderTel;
            shipmentLog.BusinessUserId = shipment.BusinessUserId;
            //shipmentLog.BranchId = iuser.CompanyBranchId;
            await _context.ShipmentLogs.AddAsync(shipmentLog);
            await _context.SaveChangesAsync();

            SessionAddRemark sessionAddRemark = new SessionAddRemark();
            sessionAddRemark.ShipmentId = shipment.Id;
            sessionAddRemark.UserId = shipment.UserId;
            sessionAddRemark.BranchId = shipment.UserId;
            sessionAddRemark.EntryDateTime = DateTime.Now;
            sessionAddRemark.NewStatus = shipment.Status;
            sessionAddRemark.Remark = shipment.Alert;
            sessionAddRemark.EntryDate = DateTime.Now;
            //sessionAddRemark.BranchId = iuser.CompanyBranchId;
            await _context.Session_Add_Remarks.AddAsync(sessionAddRemark);
            await _context.SaveChangesAsync();
            if (createShipmentViewModel.IsReturn)
            {
                var ex = await _context.ShipmentStatuses.Where(t => t.ExStart == true).FirstOrDefaultAsync();
                Shipment shipmentRet = new Shipment();
                shipmentRet = shipment;

                shipmentRet.IsForeign = createShipmentViewModel.IsForeign;
                shipmentRet.ShipmentQuantity = createShipmentViewModel.ShipmentQuantity;
                shipmentRet.ShipmentTrackingNo = shipment.ShipmentTrackingNo + "_ex";
                shipmentRet.IsReturn = false;
                shipmentRet.ShipmentFees = 0;
                shipmentRet.ShipmentExtraFees = 0;
                shipmentRet.Status = ex.Id;
                shipmentRet.ShipmentTotal = 0;
                shipmentRet.ShipmentPrice = 0;
                shipmentRet.Id = 0;
                await _context.Shipments.AddAsync(shipmentRet);
                await _context.SaveChangesAsync();
                ShipmentLog shipmentLogwx = new ShipmentLog();
                shipmentLogwx.Id = 0;
                shipmentLogwx.ShipmentId = shipmentRet.Id;
                shipmentLogwx.EntryDate = DateTime.Now;
                shipmentLogwx.EntryDateTine = DateTime.Now;
                shipmentLogwx.UserId = shipmentRet.UserId;
                shipmentLogwx.Status = ex.Id;
                shipmentLogwx.ClientName = shipmentRet.ClientName;
                shipmentLogwx.ClientPhone = shipmentRet.ClientPhone;
                shipmentLogwx.ClientCityId = shipmentRet.ClientCityId;
                shipmentLogwx.ClientAreaId = shipmentRet.ClientCityId;
                shipmentLogwx.ClientAreaId = shipmentRet.ClientAreaId;
                shipmentLogwx.IsUserBusiness = shipmentRet.IsUserBusiness;
                shipmentLogwx.SenderName = shipmentRet.SenderName;
                shipmentLogwx.SenderTel = shipmentRet.SenderTel;
                shipmentLogwx.BusinessUserId = shipmentRet.BusinessUserId;
                shipmentLogwx.BranchId = iuser.CompanyBranchId;
                //await _context.ShipmentLogs.AddAsync(shipmentLogwx);
                await _context.SaveChangesAsync();

                SessionAddRemark sessionAddRemarkLogwx = new SessionAddRemark();
                sessionAddRemarkLogwx.ShipmentId = shipmentLogwx.Id;
                sessionAddRemarkLogwx.UserId = shipment.UserId;
                sessionAddRemarkLogwx.BranchId = shipment.UserId;
                sessionAddRemarkLogwx.EntryDateTime = DateTime.Now;
                sessionAddRemarkLogwx.NewStatus = ex.Id;
                sessionAddRemarkLogwx.Remark = shipment.Alert;
                sessionAddRemarkLogwx.EntryDate = DateTime.Now;
                //sessionAddRemarkLogwx.BranchId = iuser.CompanyBranchId;
                await _context.Session_Add_Remarks.AddAsync(sessionAddRemarkLogwx);
                await _context.SaveChangesAsync();
            }
        }
        public async Task GcreateNewShipmentsDraft(BussCreateShipmentViewModel createShipmentViewModel, int userId, int MarketerId)
        {
            var iuser = await _context.Users.FirstOrDefaultAsync(t => t.Id == createShipmentViewModel.BusinessUserID);
            var user = await _context.Users.FindAsync(createShipmentViewModel.BusinessUserID);
            Shipment shipment = new Shipment();
            shipment.MarketerId = MarketerId;
            shipment.IsForeign = createShipmentViewModel.IsForeign;
            shipment.ShipmentQuantity = createShipmentViewModel.ShipmentQuantity;
            shipment.Alert = createShipmentViewModel.Alert;
            shipment.BusinessUserId = createShipmentViewModel.BusinessUserID;
            shipment.ClientName = createShipmentViewModel.ClientName;
            shipment.ClientAddress = createShipmentViewModel.ClientAddress;
            shipment.ShipmentTypeId = createShipmentViewModel.ShipmentTypeID;
            shipment.ClientLandLine = createShipmentViewModel.ClientAddress;
            shipment.ClientAreaId = Convert.ToInt32(createShipmentViewModel.ClientAreaID);
            shipment.ClientCityId = Convert.ToInt32(createShipmentViewModel.ClientCityID);
            shipment.ClientPhone = createShipmentViewModel.ClientPhone;
            shipment.ClientPhone2 = createShipmentViewModel.ClientPhone2;
            shipment.FromCityId = user.CityId;
            shipment.ShipmentContains = createShipmentViewModel.ShipmentContains;
            shipment.EntryDateTime = DateTime.Now;
            shipment.EntryDate = DateTime.Now;
            shipment.IsReturn = createShipmentViewModel.IsReturn;
            shipment.Remarks = createShipmentViewModel.Remarks;
            shipment.LastUpdate = DateTime.Now;
            shipment.SenderName = $"{user.FirstName} {user.LastName}";
            shipment.SenderTel = user.MobileNo1;
            shipment.Status = (int?)StatusEnum.Draft;
            shipment.ShipmentTrackingNo = createShipmentViewModel.ShipmentTrackingNo;
            shipment.ShipmentFees = createShipmentViewModel.ShipmentFees;
            // shipment.ShipmentExtraFees = createShipmentViewModel.ShipmentExtraFees;
            shipment.ShipmentTotal = createShipmentViewModel.ShipmentTotal;
            shipment.lang = createShipmentViewModel.lang;
            shipment.ShipmentPrice = createShipmentViewModel.ShipmentPrice;
            shipment.UserId = userId;
            shipment.rangeVal = 1;
            shipment.RetPay = createShipmentViewModel.RetPay;
            shipment.BranchId = user.CompanyBranchId;
            shipment.ClientChatUrl = createShipmentViewModel.ClientChatUrl;
            shipment.ClientMapAddress = createShipmentViewModel.ClientMapAddress;
            shipment.OldShipmentPrice = shipment.ShipmentPrice;
            shipment.DriverCanOpenShipment = createShipmentViewModel.DriverCanOpenShipment;
            await _context.Shipments.AddAsync(shipment);
            await _context.SaveChangesAsync();
            await BusinessClient(shipment);
            ShipmentLog shipmentLog = new ShipmentLog();
            shipmentLog.ShipmentId = shipment.Id;
            shipmentLog.EntryDate = DateTime.Now;
            shipmentLog.EntryDateTine = DateTime.Now;
            shipmentLog.UserId = shipment.UserId;
            shipmentLog.Status = shipment.Status;
            shipmentLog.ClientName = shipment.ClientName;
            shipmentLog.ClientPhone = shipment.ClientPhone;
            shipmentLog.ClientAreaId = shipment.ClientAreaId;
            shipmentLog.ClientCityId = shipment.ClientCityId;
            shipmentLog.IsUserBusiness = shipment.IsUserBusiness;
            shipmentLog.SenderName = shipment.SenderName;
            shipmentLog.SenderTel = shipment.SenderTel;
            shipmentLog.BusinessUserId = shipment.BusinessUserId;
            //shipmentLog.BranchId = iuser.CompanyBranchId;
            await _context.ShipmentLogs.AddAsync(shipmentLog);
            await _context.SaveChangesAsync();

            SessionAddRemark sessionAddRemark = new SessionAddRemark();
            sessionAddRemark.ShipmentId = shipment.Id;
            sessionAddRemark.UserId = shipment.UserId;
            sessionAddRemark.BranchId = shipment.UserId;
            sessionAddRemark.EntryDateTime = DateTime.Now;
            sessionAddRemark.NewStatus = shipment.Status;
            sessionAddRemark.Remark = shipment.Alert;
            sessionAddRemark.EntryDate = DateTime.Now;
            //sessionAddRemark.BranchId = iuser.CompanyBranchId;
            await _context.Session_Add_Remarks.AddAsync(sessionAddRemark);
            await _context.SaveChangesAsync();
            if (createShipmentViewModel.IsReturn)
            {
                var ex = await _context.ShipmentStatuses.Where(t => t.ExStart == true).FirstOrDefaultAsync();
                Shipment shipmentRet = new Shipment();
                shipmentRet = shipment;

                shipmentRet.IsForeign = createShipmentViewModel.IsForeign;
                shipmentRet.ShipmentQuantity = createShipmentViewModel.ShipmentQuantity;
                shipmentRet.ShipmentTrackingNo = shipment.ShipmentTrackingNo + "_ex";
                shipmentRet.IsReturn = false;
                shipmentRet.ShipmentFees = 0;
                shipmentRet.ShipmentExtraFees = 0;
                shipmentRet.Status = ex.Id;
                shipmentRet.ShipmentTotal = 0;
                shipmentRet.ShipmentPrice = 0;
                shipmentRet.Id = 0;
                await _context.Shipments.AddAsync(shipmentRet);
                await _context.SaveChangesAsync();
                ShipmentLog shipmentLogwx = new ShipmentLog();
                shipmentLogwx.Id = 0;
                shipmentLogwx.ShipmentId = shipmentRet.Id;
                shipmentLogwx.EntryDate = DateTime.Now;
                shipmentLogwx.EntryDateTine = DateTime.Now;
                shipmentLogwx.UserId = shipmentRet.UserId;
                shipmentLogwx.Status = ex.Id;
                shipmentLogwx.ClientName = shipmentRet.ClientName;
                shipmentLogwx.ClientPhone = shipmentRet.ClientPhone;
                shipmentLogwx.ClientCityId = shipmentRet.ClientCityId;
                shipmentLogwx.ClientAreaId = shipmentRet.ClientCityId;
                shipmentLogwx.ClientAreaId = shipmentRet.ClientAreaId;
                shipmentLogwx.IsUserBusiness = shipmentRet.IsUserBusiness;
                shipmentLogwx.SenderName = shipmentRet.SenderName;
                shipmentLogwx.SenderTel = shipmentRet.SenderTel;
                shipmentLogwx.BusinessUserId = shipmentRet.BusinessUserId;
                //shipmentLogwx.BranchId = iuser.CompanyBranchId;
                await _context.ShipmentLogs.AddAsync(shipmentLogwx);
                await _context.SaveChangesAsync();

                SessionAddRemark sessionAddRemarkLogwx = new SessionAddRemark();
                sessionAddRemarkLogwx.ShipmentId = shipmentLogwx.Id;
                sessionAddRemarkLogwx.UserId = shipment.UserId;
                sessionAddRemarkLogwx.BranchId = shipment.UserId;
                sessionAddRemarkLogwx.EntryDateTime = DateTime.Now;
                sessionAddRemarkLogwx.NewStatus = ex.Id;
                sessionAddRemarkLogwx.Remark = shipment.Alert;
                sessionAddRemarkLogwx.EntryDate = DateTime.Now;
                //sessionAddRemarkLogwx.BranchId = iuser.CompanyBranchId;
                await _context.Session_Add_Remarks.AddAsync(sessionAddRemarkLogwx);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<ShipmentsTrackingGeneratedCode> GetShipmentsTrackingGeneratedCode(int UserId)
        {

            //var rowsAffected = _context.Database.ExecuteSqlRaw("EXEC  [dbo].[ShipmentTrackingGenerator]");

            SqlConnection sqlConnection = new SqlConnection(_context.Database.GetConnectionString());
            SqlCommand sqlCommand = new SqlCommand("EXEC  [dbo].[ShipmentTrackingGenerator]", sqlConnection);
            SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(sqlCommand);
            DataTable dataTable = new DataTable();
            sqlDataAdapter.Fill(dataTable);

            ShipmentsTrackingGeneratedCode shipmentsTrackingGeneratedCode = new ShipmentsTrackingGeneratedCode();

            shipmentsTrackingGeneratedCode.GeneratedCode = dataTable.Rows[0][0].ToString();
            //var cods = await _context.ShipmentsTrackingGeneratedCodes/*.Where(t => t.UserBusId == UserId)*/.OrderByDescending(t => t.Id).OrderByDescending(t => t.CodeNum).FirstOrDefaultAsync();
            //if (cods == null)
            //{
            //    shipmentsTrackingGeneratedCode.MonthId = DateTime.Now.Month;
            //    shipmentsTrackingGeneratedCode.DayId = DateTime.Now.Day;
            //    shipmentsTrackingGeneratedCode.YearId = DateTime.Now.Year;
            //    shipmentsTrackingGeneratedCode.UserBusId = UserId;
            //    shipmentsTrackingGeneratedCode.CodeNum = 1;
            //    shipmentsTrackingGeneratedCode.GeneratedCode = "R" /*+ UserId + ""*/ + shipmentsTrackingGeneratedCode.CodeNum?.ToString();
            //    await _context.ShipmentsTrackingGeneratedCodes.AddAsync(shipmentsTrackingGeneratedCode);
            //    await _context.SaveChangesAsync();
            //}
            //else
            //{
            //    shipmentsTrackingGeneratedCode.MonthId = DateTime.Now.Month;
            //    shipmentsTrackingGeneratedCode.DayId = DateTime.Now.Day;
            //    shipmentsTrackingGeneratedCode.YearId = DateTime.Now.Year;
            //    shipmentsTrackingGeneratedCode.UserBusId = UserId;
            //    shipmentsTrackingGeneratedCode.CodeNum = cods.CodeNum + 1;
            //    shipmentsTrackingGeneratedCode.GeneratedCode = "R" /*+ UserId + "" */+ shipmentsTrackingGeneratedCode.CodeNum?.ToString();
            //    await _context.ShipmentsTrackingGeneratedCodes.AddAsync(shipmentsTrackingGeneratedCode);
            //    await _context.SaveChangesAsync();
            //}
            return shipmentsTrackingGeneratedCode;
        }
    }
}
