using AccountingSystem.Data;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roadfn.Controllers;
using Roadfn.Services;
using Roadfn.ViewModel;

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


    }
}
