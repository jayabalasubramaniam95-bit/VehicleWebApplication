using VehicleManagement.ViewModels;

namespace VehicleManagement.Services;

public interface IVehicleService
{
    #region List (Search, Paging)

    VehicleListViewModel GetVehicles(string? search, int page, int pageSize, string sortColumn, string sortDirection);

    #endregion

    #region Details

    VehicleDetailsViewModel? GetDetails(int id);

    #endregion

    #region Form (Create / Edit)

    VehicleFormViewModel GetCreateViewModel();

    VehicleFormViewModel? GetEditViewModel(int id);

    /// <summary>Refills the manufacturer dropdown after a failed post.</summary>
    void PopulateDropdowns(VehicleFormViewModel model);

    ServiceResult Create(VehicleFormViewModel model);

    ServiceResult Update(VehicleFormViewModel model);

    #endregion

    #region Delete

    ServiceResult Delete(int id);

    #endregion
}
