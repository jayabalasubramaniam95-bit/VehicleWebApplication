using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleManagement.Services;
using VehicleManagement.ViewModels;

namespace VehicleManagement.Controllers;

public class ManufacturersController : Controller
{
    #region Constants

    private const int PageSize = 10;

    private const string SuccessKey = "SuccessMessage";
    private const string ErrorKey = "ErrorMessage";
    private const string FormView = "Form";

    private const string DuplicateNameMessage =
        "A manufacturer with this name already exists.";

    #endregion

    #region Dependencies & Constructor

    private readonly IManufacturerService _manufacturerService;

    public ManufacturersController(IManufacturerService manufacturerService)
    {
        _manufacturerService = manufacturerService;
    }

    #endregion

    #region List and Details

    [HttpGet]
    public ActionResult Index(string? search, int page = 1)
    {
        var viewModel = _manufacturerService.GetPageWiseManufacturer(search, page, PageSize);

        return View(viewModel);
    }

    [HttpGet]
    public ActionResult Details(int id)
    {
        var model = _manufacturerService.GetManufacturerDetails(id);

        return model is null
            ? NotFound()
            : View(model);
    }

    #endregion

    #region Create

    [HttpGet]
    public ActionResult Create()
    {
        return View(FormView, new ManufacturerFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Create(ManufacturerFormViewModel model)
    {
        ValidateUniqueName(model);

        if (!ModelState.IsValid)
        {
            return View(FormView, model);
        }

        try
        {
            _manufacturerService.Create(model);

            TempData[SuccessKey] = "Manufacturer created successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            TempData[ErrorKey] = "The manufacturer could not be saved. Please try again.";

            return View(FormView, model);
        }
    }

    #endregion

    #region Edit

    [HttpGet]
    public ActionResult Edit(int id)
    {
        var model = _manufacturerService.GetManufacturerForEdit(id);

        return model is null
            ? NotFound()
            : View(FormView, model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Edit(ManufacturerFormViewModel model)
    {
        ValidateUniqueName(model);

        if (!ModelState.IsValid)
        {
            return View(FormView, model);
        }

        try
        {
            var updated = _manufacturerService.Update(model);

            if (updated)
            {
                TempData[SuccessKey] = "Manufacturer updated successfully.";
            }
            else
            {
                TempData[ErrorKey] = "The manufacturer could not be found.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            TempData[ErrorKey] = "The manufacturer could not be updated. Please try again.";

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
            var result = _manufacturerService.Delete(id);

            switch (result)
            {
                case DeleteResult.NotFound:
                    TempData[ErrorKey] = "The manufacturer could not be found.";
                    break;

                case DeleteResult.IsDefault:
                    TempData[ErrorKey] = "The default manufacturer cannot be deleted.";
                    break;

                case DeleteResult.HasVehicles:
                    TempData[ErrorKey] =
                        "The manufacturer cannot be deleted because vehicles are associated with it.";
                    break;

                case DeleteResult.Deleted:
                    TempData[SuccessKey] = "Manufacturer deleted successfully.";
                    break;
            }
        }
        catch (DbUpdateException)
        {
            TempData[ErrorKey] = "The manufacturer could not be deleted. Please try again.";
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
            !_manufacturerService.IsManufacturersNameExists(name, id == 0 ? null : id);

        return Json(isAvailable);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Performs the server-side duplicate-name check.
    /// Remote validation alone can be bypassed.
    /// </summary>
    private void ValidateUniqueName(ManufacturerFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return;
        }

        var excludeId = model.IsEdit ? model.Id : (int?)null;

        if (_manufacturerService.IsManufacturersNameExists(model.Name, excludeId))
        {
            ModelState.AddModelError(nameof(model.Name), DuplicateNameMessage);
        }
    }

    #endregion
}