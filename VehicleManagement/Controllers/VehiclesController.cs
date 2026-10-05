using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleManagement.Services;
using VehicleManagement.ViewModels;

namespace VehicleManagement.Controllers;

public class VehiclesController : Controller
{
    #region Constants

    private const int PageSize = 10;
    private const string SuccessKey = "SuccessMessage";
    private const string ErrorKey = "ErrorMessage";
    private const string FormView = "Form";

    private const string DatabaseErrorMessage =
        "The operation could not be completed. Please try again.";

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
    public ActionResult Index(
        string? search,
        int page = 1,
        string sortColumn = "owner",
        string sortDirection = "asc")
    {
        var viewModel = _vehicleService.GetVehicles(
            search,
            page,
            PageSize,
            sortColumn,
            sortDirection);

        return View(viewModel);
    }

    #endregion

    #region Details

    [HttpGet]
    public ActionResult Details(int id)
    {
        return ViewOrNotFound(_vehicleService.GetDetails(id));
    }

    #endregion

    #region Create

    [HttpGet]
    public ActionResult Create()
    {
        return View(FormView, _vehicleService.GetCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Create(VehicleFormViewModel model)
    {
        return Save(model, _vehicleService.Create, "Vehicle created successfully.");
    }

    #endregion

    #region Edit

    [HttpGet]
    public ActionResult Edit(int id)
    {
        return ViewOrNotFound(_vehicleService.GetEditViewModel(id), FormView);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Edit(VehicleFormViewModel model)
    {
        return Save(model, _vehicleService.Update, "Vehicle updated successfully.");
    }

    #endregion

    #region Delete

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Delete(int id)
    {
        try
        {
            var result = _vehicleService.Delete(id);

            return result.Success
                ? RedirectWithSuccess("Vehicle deleted successfully.")
                : RedirectWithError(result.ErrorMessage);
        }
        catch (DbUpdateException)
        {
            return RedirectWithError(DatabaseErrorMessage);
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Shared post-back flow for Create and Edit.
    /// Handles validation, service errors and database failures.
    /// </summary>
    private ActionResult Save(
        VehicleFormViewModel model,
        Func<VehicleFormViewModel, ServiceResult> save,
        string successMessage)
    {
        if (!ModelState.IsValid)
        {
            return RedisplayForm(model);
        }

        try
        {
            var result = save(model);

            if (result.Success)
            {
                return RedirectWithSuccess(successMessage);
            }

            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "The vehicle could not be saved.");

            return RedisplayForm(model);
        }
        catch (DbUpdateException)
        {
            return RedirectWithError(DatabaseErrorMessage);
        }
    }

    private ActionResult RedisplayForm(VehicleFormViewModel model)
    {
        _vehicleService.PopulateDropdowns(model);
        return View(FormView, model);
    }

    private ActionResult ViewOrNotFound<TModel>(TModel? model, string? viewName = null)
        where TModel : class
    {
        return model is null
            ? NotFound()
            : View(viewName, model);
    }

    private ActionResult RedirectWithSuccess(string message)
    {
        TempData[SuccessKey] = message;
        return RedirectToAction(nameof(Index));
    }

    private ActionResult RedirectWithError(string? message)
    {
        TempData[ErrorKey] = message;
        return RedirectToAction(nameof(Index));
    }

    #endregion
}