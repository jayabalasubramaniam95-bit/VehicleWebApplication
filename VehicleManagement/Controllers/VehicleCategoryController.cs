using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleManagement.Services;
using VehicleManagement.ViewModels;

namespace VehicleManagement.Controllers;

public class VehicleCategoryController : Controller
{
    #region Constants

    private const string SuccessKey = "SuccessMessage";
    private const string ErrorKey = "ErrorMessage";
    private const string FormView = "Form";

    private const string DuplicateNameMessage =
        "A category with this name already exists.";

    #endregion

    #region Dependencies & Constructor

    private readonly IVehicleCategoryService _categoryService;

    public VehicleCategoryController(IVehicleCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    #endregion

    #region List

    [HttpGet]
    public ActionResult Index()
    {
        return View(_categoryService.GetVehicleCategoryList());
    }

    #endregion

    #region Create

    [HttpGet]
    public ActionResult Create()
    {
        return View(FormView, new VehicleCategoryFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Create(VehicleCategoryFormViewModel model)
    {
        ValidateUniqueName(model);

        if (!ModelState.IsValid)
        {
            return View(FormView, model);
        }

        try
        {
            var result = _categoryService.Create(model);

            return CompleteSave(result, model, "created");
        }
        catch (DbUpdateException)
        {
            TempData[ErrorKey] = "The vehicle category could not be saved. Please try again.";

            return View(FormView, model);
        }
    }

    #endregion

    #region Edit

    [HttpGet]
    public ActionResult Edit(int id)
    {
        var model = _categoryService.GetVehicleCategoryForEdit(id);

        return model is null
            ? NotFound()
            : View(FormView, model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Edit(VehicleCategoryFormViewModel model)
    {
        ValidateUniqueName(model);

        if (!ModelState.IsValid)
        {
            return View(FormView, model);
        }

        try
        {
            var result = _categoryService.Update(model);

            return CompleteSave(result, model, "updated");
        }
        catch (DbUpdateException)
        {
            TempData[ErrorKey] = "The vehicle category could not be updated. Please try again.";

            return View(FormView, model);
        }
    }

    #endregion

    #region Delete

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Delete(int id)
    {
        try
        {
            var result = _categoryService.Delete(id);

            switch (result)
            {
                case CategoryDeleteResult.NotFound:
                    TempData[ErrorKey] = "The vehicle category could not be found.";
                    break;

                case CategoryDeleteResult.HasVehicles:
                    TempData[ErrorKey] =
                        "The vehicle category cannot be deleted because vehicles are associated with it.";
                    break;

                case CategoryDeleteResult.LastCategory:
                    TempData[ErrorKey] = "The last vehicle category cannot be deleted.";
                    break;

                case CategoryDeleteResult.Deleted:
                    TempData[SuccessKey] = "Vehicle category deleted successfully.";
                    break;
            }
        }
        catch (DbUpdateException)
        {
            TempData[ErrorKey] = "The vehicle category could not be deleted. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    #endregion

    #region Remote Validation

    [AcceptVerbs("GET", "POST")]
    public ActionResult IsNameAvailable(string name, int id)
    {
        var isAvailable =
            string.IsNullOrWhiteSpace(name) ||
            !_categoryService.IsVehicleCategoryNameExists(name, id == 0 ? null : id);

        return Json(isAvailable);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Performs the server-side duplicate-name check.
    /// Remote validation can be bypassed by direct HTTP requests,
    /// so server-side validation is also required.
    /// </summary>
    private void ValidateUniqueName(VehicleCategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return;
        }

        var excludeId = model.IsEdit ? model.Id : (int?)null;

        if (_categoryService.IsVehicleCategoryNameExists(model.Name, excludeId))
        {
            ModelState.AddModelError(nameof(model.Name), DuplicateNameMessage);
        }
    }

    private ActionResult CompleteSave(
        CategorySaveResult result,
        VehicleCategoryFormViewModel model,
        string action)
    {
        switch (result.Status)
        {
            case CategorySaveStatus.NotFound:
                return NotFound();

            case CategorySaveStatus.InvalidRange:
                ModelState.AddModelError(
                    string.Empty,
                    result.Error ?? "The category configuration is invalid.");

                return View(FormView, model);

            default:
                TempData[SuccessKey] =
                    $"Vehicle category '{model.Name}' was {action}. " +
                    "Existing vehicles were re-categorised.";

                return RedirectToAction(nameof(Index));
        }
    }

    #endregion
}