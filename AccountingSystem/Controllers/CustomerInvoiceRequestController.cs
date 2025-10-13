using AccountingSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roadfn.Models;
using Syncfusion.EJ2.Base;

namespace Roadfn.Controllers
{


    [Authorize(Policy = "accountmanagement.busnissstatment")]
    public class CustomerInvoiceRequestController : Controller
    {
        private RoadFnDbContext _context;
        public CustomerInvoiceRequestController(RoadFnDbContext context)
        {
            _context = context;
        }
        public IActionResult List()
        {
            return View();
        }

        public IActionResult UrlDatasource([FromBody] DataManagerRequest dm)
        {
            IEnumerable<CustomerInvoiceRequest> DataSource = _context.CustomerInvoiceRequest.Where(t => t.RecStatus == "New").AsEnumerable();
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
        public async Task<IActionResult> ChangeStatus(int Id)
        {
            var reques = await _context.CustomerInvoiceRequest.FirstOrDefaultAsync(t => t.Id == Id);

            reques.RecStatus = "Done";

            _context.CustomerInvoiceRequest.Update(reques);
            await _context.SaveChangesAsync();
            return Ok("Done");
        }

    }
}
