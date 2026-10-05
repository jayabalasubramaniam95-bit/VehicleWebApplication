using Microsoft.AspNetCore.Mvc;
using VehicleManagement.Services;
using VehicleManagement.ViewModels;

namespace VehicleManagement.Controllers;

public class VehiclesController : Controller
{
    #region Constants

    private const int PageSize = 10;
    private const string SuccessKey = "SuccessMessage";
    private const string ErrorKey = "ErrorMessage";

    #endregion

    #region Dependencies & Constructor

    private readonly IVehicleService _vehicleService;

    public VehiclesController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    #endregion

    #region List (Index)

    [HttpGet]
    public ActionResult Index(string? search, int page = 1, string sortColumn = "owner", string sortDirection = "asc") =>
        View(_vehicleService.GetVehicles(search, page, PageSize, sortColumn, sortDirection));

    #endregion

    #region Details

    [HttpGet]
    public ActionResult Details(int id) =>
        ViewOrNotFound(_vehicleService.GetDetails(id));

    #endregion

    #region Create

    [HttpGet]
    public ActionResult Create() =>
        View("Form", _vehicleService.GetCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public ActionResult Create(VehicleFormViewModel model) =>
        Save(model, _vehicleService.Create, "Vehicle was created successfully.");

    #endregion

    #region Edit

    [HttpGet]
    public ActionResult Edit(int id) =>
        ViewOrNotFound(_vehicleService.GetEditViewModel(id), "Form");

    [HttpPost, ValidateAntiForgeryToken]
    public ActionResult Edit(VehicleFormViewModel model) =>
        Save(model, _vehicleService.Update, "Vehicle was updated successfully.");

    #endregion

    #region Delete (confirmed via popup on the Index page, POST only)

    [HttpPost, ValidateAntiForgeryToken]
    public ActionResult Delete(int id)
    {
        var result = _vehicleService.Delete(id);

        return result.Success
            ? RedirectWithSuccess("Vehicle was deleted successfully.")
            : RedirectWithError(result.ErrorMessage!);
    }

    #endregion

    #region Helpers

    /// <summary>Shared post-back flow for Create and Edit.</summary>
    private ActionResult Save(
        VehicleFormViewModel model,
        Func<VehicleFormViewModel, ServiceResult> save,
        string successMessage)
    {
        if (ModelState.IsValid)
        {
            var result = save(model);

            if (result.Success)
            {
                return RedirectWithSuccess(successMessage);
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
        }

        _vehicleService.PopulateDropdowns(model);
        return View("Form", model);
    }

    private ActionResult ViewOrNotFound<TModel>(TModel? model, string? viewName = null) where TModel : class =>
        model is null ? NotFound() : View(viewName, model);

    private ActionResult RedirectWithSuccess(string message)
    {
        TempData[SuccessKey] = message;
        return RedirectToAction(nameof(Index));
    }

    private ActionResult RedirectWithError(string message)
    {
        TempData[ErrorKey] = message;
        return RedirectToAction(nameof(Index));
    }

    #endregion
}
