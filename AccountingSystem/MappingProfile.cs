using AutoMapper;
using Roadfn.Models;
using Roadfn.ViewModel;

namespace AccountingSystem
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Shipment, CreateShipmentViewModel>();
            CreateMap<BussCreateShipmentViewModel, ApiBussCreateShipmentViewModel>();
            CreateMap<ApiBussCreateShipmentViewModel, BussCreateShipmentViewModel>();
            CreateMap<ApiCreateShipmentViewModel, BussCreateShipmentViewModel>();
            CreateMap<BussCreateShipmentViewModel, ApiCreateShipmentViewModel>();
            CreateMap<CreateShipmentViewModel, Shipment>();
            CreateMap<UpdateShipmentAdminViewModel, Shipment>();
            CreateMap<UpdateShipmentViewModel, Shipment>();
            CreateMap<UpdateShipmentViewModelbUSS, Shipment>();
            CreateMap<Shipment, UpdateShipmentViewModelbUSS>();
            CreateMap<Shipment, UpdateShipmentAdminViewModel>();
            CreateMap<Shipment, UpdateShipmentViewModel>();
            CreateMap<UpdateShipmentViewModel, UpdateShipmentViewModel>();
            CreateMap<ShipmentSummaryListViewModel, ShipmentSummary>();
            CreateMap<ShipmentSummary, ShipmentSummaryListViewModel>();
        }
    }
}
