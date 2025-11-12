using AccountingSystem.Data;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roadfn.Models;
using Roadfn.Services;
using Roadfn.ViewModel;
using Syncfusion.EJ2.Base;
using System.Data;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{

    [Authorize]

    public class RoadfnLogsController : Controller
    {
        private RoadFnDbContext _context;
        public RoadfnLogsController(RoadFnDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult UrlDatasource([FromBody] DataManagerRequest dm, string Application, string Id)
        {
            var DataSource = _context.EntitiesChanges.Where(t => t.TableName == Application && t.EntityId == Id).AsQueryable();
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
    }
}
