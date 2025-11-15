using AccountingSystem.Data;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roadfn.Controllers;
using Roadfn.Models;
using Roadfn.Services;
using Roadfn.ViewModel;
using System.Security.Claims;
using System.Text.Json;

namespace AccountingSystem.Controllers
{
    public class ShipmentsController : Controller
    {
        private readonly ILogger<HomeController> _log;
        private RoadFnDbContext _context;
        private IShipmentService _shipmentService;
        private readonly IMapper _mapper;
        private readonly IWebHostEnvironment _env;
        public ShipmentsController(ILogger<HomeController> log, RoadFnDbContext context, IShipmentService shipmentService, IMapper mapper, IWebHostEnvironment env)
        {
            _log = log;

            _context = context;
            _shipmentService = shipmentService;
            _mapper = mapper;
            _env = env;
        }
        public async Task<IActionResult> GetAlert(int ShipmentTypeId)
        {
            var ShipmentsTypes = await _context.ShipmentsTypes.Where(t => t.Id == ShipmentTypeId).FirstOrDefaultAsync();
            return Ok(ShipmentsTypes);
        }

        public async Task<IActionResult> GetShipmentByClinteMobile(string Mobile)
        {
            var t = await _context.GetShipmentByClinteMobileView.Where(t => t.ClientPhone == Mobile && t.NewStatus == (int?)StatusEnum.CODPickup).FirstOrDefaultAsync();
            var t2 = await _context.GetShipmentByClinteMobileView.Where(t => t.ClientPhone == Mobile && t.NewStatus == (int?)StatusEnum.Returned).FirstOrDefaultAsync();
            var ShipmentsTypesClose = 0;
            var ShipmentsTypesRet = 0;
            if (t != null)
            {
                ShipmentsTypesClose = t.TheCount;
            }
            if (t2 != null)
            {
                ShipmentsTypesRet = t2.TheCount;
            }

            return Ok(new { ShipmentsTypesClose = ShipmentsTypesClose, ShipmentsTypesRet = ShipmentsTypesRet });
        }


        public async Task<IActionResult> Update(int Id)
        {
            var shipment = await _context.Shipments.FindAsync(Id);
            if (shipment == null)
            {
                return NotFound();

            }

            //if (shipment.Status == Convert.ToInt32(StatusEnum.Draft) || shipment.Status == Convert.ToInt32(StatusEnum.Submitted))
            //{
            var t = from t1 in _context.Users
                    where t1.UserType == "3"
                    select new { t1.Id, Name = $"{t1.UserName}-{t1.FirstName} {t1.LastName}" };
            ViewBag.ShipmentType = await _context.ShipmentsTypes.ToListAsync();
            ViewBag.city = await _context.Cities.ToListAsync();
            ViewBag.area = await _context.Areas.ToListAsync();
            ViewBag.ShipmentColorStatus = await _context.ShipmentColorStatus.ToListAsync();
            ViewBag.UsersBus = t;

            UpdateShipmentViewModel updateShipmentViewModel = new UpdateShipmentViewModel();
            var ss = _mapper.Map<UpdateShipmentViewModel>(shipment);
            // return View(ss);
            //ss.IsForeign =Convert.ToBoolean( ss.IsForeigne) ?? false;

            if (ss.IsForeign == null)
            {
                ss.IsForeign = false;
            }
            return PartialView("_Update", ss);
            //}
            //else
            //{
            //    return NotFound();

            //}
        }

        [HttpPost]
        public async Task<IActionResult> Update(UpdateShipmentViewModel updateShipmentViewModel)
        {
            //var t = from t1 in _context.Users
            //        where t1.UserType == "3"
            //        select new { t1.Id, Name = $"{t1.UserName}-{t1.FirstName} {t1.LastName}" };
            //ViewBag.ShipmentType = await _context.ShipmentsTypes.ToListAsync();
            //ViewBag.city = await _context.Cities.ToListAsync();
            //ViewBag.area = await _context.Areas.ToListAsync();
            //ViewBag.UsersBus = t;

            if (!ModelState.IsValid)
            {
                return BadRequest("الرجاء التأكد من المعلومات");
            }
            var shipment = await _context.Shipments.FindAsync(updateShipmentViewModel.ID);
            var user = await _context.Users.FindAsync(updateShipmentViewModel.BusinessUserID);
            var ststus = await _context.ShipmentStatus.Where(t => t.Id == shipment.Status).FirstOrDefaultAsync();
            if (ststus.EnableEditForAdmin == false)
            {
                return Ok("لايمكن تحديث معلومات الشحنة يرجى التوصل مع System Support");
            }
            string jsonString = JsonSerializer.Serialize(updateShipmentViewModel);

            string userId = User.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;

            var oldprice = shipment.ShipmentPrice;
            var oldShipmentTotal = shipment.ShipmentTotal;

            _log.LogInformation($"[UpdateShipment (admin)  {User.Identity.Name}{Environment.NewLine}{jsonString}{Environment.NewLine} ]");

            shipment.Alert = updateShipmentViewModel.Alert;
            shipment.BusinessUserId = updateShipmentViewModel.BusinessUserID;
            shipment.ClientName = updateShipmentViewModel.ClientName;
            shipment.ShipmentTypeId = updateShipmentViewModel.ShipmentTypeID;
            shipment.ShipmentQuantity = updateShipmentViewModel.ShipmentQuantity;
            shipment.IsForeign = updateShipmentViewModel.IsForeign;
            shipment.DriverCanOpenShipment = updateShipmentViewModel.DriverCanOpenShipment;
            shipment.ClientLandLine = updateShipmentViewModel.ClientAddress;
            shipment.ClientAddress = updateShipmentViewModel.ClientAddress;
            shipment.ClientAreaId = Convert.ToInt32(updateShipmentViewModel.ClientAreaID);
            shipment.ClientCityId = Convert.ToInt32(updateShipmentViewModel.ClientCityID);
            shipment.ClientPhone = updateShipmentViewModel.ClientPhone;
            shipment.ClientPhone2 = updateShipmentViewModel.ClientPhone2;
            shipment.FromCityId = user.CityId;
            shipment.IsReturn = updateShipmentViewModel.IsReturn;
            shipment.Remarks = updateShipmentViewModel.Remarks;
            shipment.ShipmentFeesDiscount = updateShipmentViewModel.ShipmentFeesDiscount;
            shipment.LastUpdate = DateTime.Now;
            shipment.SenderName = $"{user.FirstName} {user.LastName}";
            shipment.SenderTel = user.MobileNo1;
            shipment.ShipmentTrackingNo = updateShipmentViewModel.ShipmentTrackingNo;
            shipment.ShipmentFees = updateShipmentViewModel.ShipmentFees;
            shipment.ShipmentContains = updateShipmentViewModel.ShipmentContains;
            shipment.ShipmentExtraFees = updateShipmentViewModel.ShipmentExtraFees;
            shipment.ShipmentTotal = updateShipmentViewModel.ShipmentTotal;
            shipment.ShipmentPrice = updateShipmentViewModel.ShipmentPrice;
            shipment.RetPay = updateShipmentViewModel.RetPay;
            shipment.rangeVal = updateShipmentViewModel.rangeVal;
            shipment.PaidAmountFromShipmentFees = updateShipmentViewModel.PaidAmountFromShipmentFees;

            shipment.LastUpdate = DateTime.Now;

            if (updateShipmentViewModel.rangeVal < 0 || updateShipmentViewModel.rangeVal == 0)
            {
                updateShipmentViewModel.rangeVal = 1;
            }

            if (updateShipmentViewModel.RetPay)
            {
                if (updateShipmentViewModel.ShipmentTotal > 0)
                {
                    updateShipmentViewModel.ShipmentTotal = updateShipmentViewModel.ShipmentTotal * -1;
                }
            }

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


            //var fee = await _context.ShipmentFees.Where(t => t.UserBusinessId == updateShipmentViewModel.BusinessUserID && t.ToCityId == Convert.ToInt32(updateShipmentViewModel.ClientCityID) && t.IsBusiness == true).FirstOrDefaultAsync();
            //shipment.ShipmentFees = Convert.ToDecimal(fee.Fees);
            //shipment.ShipmentPrice = shipment.ShipmentTotal - Convert.ToDecimal(fee.Fees) - updateShipmentViewModel.ShipmentExtraFees;

            if (shipment.ShipmentTrackingNo.EndsWith("_ex"))
            {
                shipment.ShipmentExtraFees = 0;
                shipment.ShipmentTotal = 0;
                shipment.ReturnFees = 0;
                shipment.ShipmentFees = 0;
                shipment.ShipmentFeesDiscount = 0;
                shipment.ShipmentPrice = 0;
                shipment.ShipmentPriceWithDetail = 0;
            }

            if (oldShipmentTotal != shipment.ShipmentTotal)
            {
                if (shipment.ShipmentTotal < shipment.ShipmentFees)
                {
                    return Ok("لا يمكن تعديل المبلغ اقل من رسوم التوصيل ");

                }

                SessionAddRemark SessionAddRemarks = new SessionAddRemark();
                SessionAddRemarks.UserId = 0;
                SessionAddRemarks.UserIDName = userId;
                SessionAddRemarks.OldStatus = 0;
                SessionAddRemarks.ShipmentId = shipment.Id;
                SessionAddRemarks.NewStatus = 0;
                SessionAddRemarks.BranchId = 0;
                SessionAddRemarks.Remark = $"تم تعديل مبلغ التحصيل  من {oldShipmentTotal}  الى {shipment.ShipmentTotal} ";
                SessionAddRemarks.DriverAssignRemarkId = 0;
                await _context.Session_Add_Remarks.AddAsync(SessionAddRemarks);
                await _context.SaveChangesAsync();
            }


            _context.Shipments.Update(shipment);
            var ss = _mapper.Map<UpdateShipmentViewModel>(shipment);

            await _context.SaveChangesAsync();

            if (updateShipmentViewModel.IsReturn && !shipment.ShipmentTrackingNo.Contains("_ex"))
            {

                var ex = await _context.Shipments.Where(t => t.ShipmentTrackingNo == shipment.ShipmentTrackingNo + "_ex").FirstOrDefaultAsync();


                if (ex != null)
                {
                    ex.Alert = shipment.Alert;
                    ex.BusinessUserId = shipment.BusinessUserId;
                    ex.ClientName = shipment.ClientName;
                    ex.ShipmentTypeId = shipment.ShipmentTypeId;
                    ex.ClientLandLine = shipment.ClientAddress;
                    ex.ClientAddress = shipment.ClientAddress;
                    ex.ClientAreaId = Convert.ToInt32(shipment.ClientAreaId);
                    ex.ClientCityId = Convert.ToInt32(shipment.ClientCityId);
                    ex.ClientPhone = shipment.ClientPhone;
                    ex.ClientPhone2 = shipment.ClientPhone2;
                    ex.FromCityId = shipment.FromCityId;
                    ex.Remarks = updateShipmentViewModel.Remarks;
                    ex.LastUpdate = DateTime.Now;
                    ex.SenderName = shipment.SenderName;
                    ex.SenderTel = shipment.SenderTel;
                    ex.ShipmentTrackingNo = shipment.ShipmentTrackingNo + "_ex";
                    ex.ShipmentFees = 0;
                    ex.ShipmentExtraFees = 0;
                    ex.ShipmentTotal = 0;
                    ex.ShipmentPrice = 0;

                    ex.LastUpdate = DateTime.Now;
                    _context.Shipments.Update(ex);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    Shipment shipmentRet = new Shipment();
                    shipmentRet = shipment;
                    shipmentRet.Id = 0;
                    shipmentRet.ShipmentTrackingNo = shipment.ShipmentTrackingNo + "_ex";
                    shipmentRet.IsReturn = false;
                    shipmentRet.ShipmentFees = 0;
                    shipmentRet.ShipmentExtraFees = 0;
                    shipmentRet.ShipmentTotal = 0;
                    shipmentRet.ShipmentPrice = 0;

                    await _context.Shipments.AddAsync(shipmentRet);
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                var shipmentex = await _context.Shipments.Where(t => t.ShipmentTrackingNo == shipment.ShipmentTrackingNo + "_ex").FirstOrDefaultAsync();
                if (shipmentex != null)
                {
                    _context.Shipments.Remove(shipmentex);
                    await _context.SaveChangesAsync();
                }



            }
            return Ok("تم التحديث بنجاح");
        }

        public async Task<IActionResult> GetShipmentFee(int UserId, int CityId, int? AreaId)
        {
            try
            {
                var feeResult = await _shipmentService.ResolveBusinessShipmentFeesAsync(UserId, CityId, AreaId);
                return Ok(new
                {
                    Fees = feeResult.DeliveryFee,
                    ReturnFees = feeResult.ReturnFee
                });
            }
            catch (InvalidOperationException)
            {
                return Ok("NotFound");
            }
        }
        [HttpPost]
        public async Task<IActionResult> UpdateShExtraFees(int Id, decimal ExtraFees)
        {

            var sh = await _context.Shipments.FirstOrDefaultAsync(t => t.Id == Id && t.Status == 9);
            if (ExtraFees < 0)
            {
                return BadRequest("لايمكن التعديل");

            }
            if (sh != null)
            {





                // shipment.ShipmentPrice = shipment.ShipmentTotal - shipment.ShipmentExtraFees - shipment.ShipmentFees;
                sh.ShipmentPrice = Convert.ToDecimal(sh.ShipmentTotal) - (Convert.ToDecimal(ExtraFees) + Convert.ToDecimal(sh.ShipmentFees));

                sh.ShipmentExtraFees = Convert.ToDecimal(ExtraFees);




                _context.Shipments.Update(sh);
                await _context.SaveChangesAsync();
                return Ok("Done");

            }
            return BadRequest("لايمكن التعديل");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateShColorStatus(int Id, int statusId)
        {

            var sh = await _context.Shipments.FirstOrDefaultAsync(t => t.Id == Id);

            if (sh != null)
            {

                var ststucolor = await _context.ShipmentColorStatus.FirstOrDefaultAsync(t => t.Id == statusId);
                sh.ShipmentColorStatus = statusId;
                _context.Shipments.Update(sh);
                await _context.SaveChangesAsync();


                string userId = User.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;

                SessionAddRemark sessionAddRemark = new SessionAddRemark();
                sessionAddRemark.ShipmentId = sh.Id;
                sessionAddRemark.UserId = 0;
                sessionAddRemark.UserIDName = userId;
                sessionAddRemark.EntryDateTime = DateTime.Now;
                sessionAddRemark.Remark = ststucolor.StatusName;
                sessionAddRemark.EntryDate = DateTime.Now;
                await _context.Session_Add_Remarks.AddAsync(sessionAddRemark);
                await _context.SaveChangesAsync();
                //  await _pushNotifications.SendNotificationForNote(shipmentDetails.Id, shipmentAddNote.Note);

                return Ok("Done");

            }
            return Ok(sh);
        }



    }
}
