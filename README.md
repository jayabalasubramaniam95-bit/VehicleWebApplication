# Vehicle Management System

A small vehicle management web application built for the CreditWorks Software Engineer assignment.

The application is built with ASP.NET Core MVC and allows users to manage vehicles, manufacturers and vehicle categories. The main focus of the implementation is keeping the vehicle category rules consistent when categories are created, updated or deleted.

The application also includes automated unit and integration tests covering the main business behaviours.

## Technology

* .NET 10
* ASP.NET Core MVC
* C#
* Entity Framework Core
* SQL Server
* Razor Views
* Bootstrap/CSS
* JavaScript/jQuery
* Dependency Injection
* xUnit
* Moq
* ASP.NET Core `WebApplicationFactory`
* SQLite in-memory database for integration tests



# 1. Setup

## Required Software

The following software is required to run the application:

* .NET 10 SDK
* SQL Server or SQL Server Express
* SQL Server Management Studio (optional)
* Git

The application was developed and tested using Visual Studio / Visual Studio Code with the .NET 10 SDK.

## Clone the Repository

powershell
git clone https://github.com/jayabalasubramaniam95-bit/VehicleWebApplication.git
cd VehicleWebApplication


## Restore Dependencies

From the solution root:

powershell
dotnet restore




# 2. Database Requirements

The main application uses SQL Server through Entity Framework Core.

The default development connection string is configured in:


VehicleManagement/appsettings.json


Example:

json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=VehicleManagement;Trusted_Connection=True;TrustServerCertificate=True;"
}


The connection string can be changed if a different SQL Server instance is being used.

The automated integration tests do **not** use the development SQL Server database. They use an isolated SQLite in-memory database.

This keeps the automated tests independent from the developer's local database.



# 3. Database Creation and Migrations

The application uses Entity Framework Core migrations.

From the solution root, move into the web application project:

powershell
cd VehicleManagement


To apply the existing migrations:

powershell
dotnet ef database update


If the Entity Framework command is not available, install the EF Core CLI tool:

powershell
dotnet tool install --global dotnet-ef


For an existing database schema change, create a new migration:

powershell
dotnet ef migrations add MigrationName


Then apply it:

powershell
dotnet ef database update


The migrations are stored under:


VehicleManagement/Migrations




# 4. Seed Data

The application contains seed data for the default manufacturers and vehicle categories.

The default vehicle categories are initially:

| Category | Minimum Weight | Maximum Weight |
| -- | -: | -: |
| Light    |           0 kg |         500 kg |
| Medium   |         500 kg |        2500 kg |
| Heavy    |        2500 kg | No upper limit |

The default manufacturers include:

* Mazda
* Mercedes
* Honda
* Ferrari
* Toyota

Default manufacturers are protected from deletion.

Fixed seed values are used rather than dynamically generated values so that Entity Framework Core migrations remain stable.



# 5. Run the Application

From the solution root:

powershell
dotnet build


Then:

powershell
dotnet run --project .\VehicleManagement\VehicleManagement.csproj


ASP.NET Core will display the application URL in the terminal.

Open the displayed URL in a browser.

The application uses the normal ASP.NET Core MVC routing configuration, with the vehicle list as the main entry point.



# 6. Application Structure

The application follows a layered architecture:


Razor Views
     ↓
Controllers
     ↓
Services
     ↓
Repositories
     ↓
Entity Framework Core
     ↓
SQL Server


## Controllers

Controllers handle HTTP requests and coordinate the UI flow.

The main controllers are:


VehiclesController
ManufacturersController
VehicleCategoryController


Controllers are intentionally kept relatively thin. Business rules are handled by the service layer.

## Services

Services contain the main application/business logic.

Examples include:


VehicleService
ManufacturerService
VehicleCategoryService


The category service is responsible for important rules such as:

* Calculating a vehicle category from its weight.
* Validating category ranges.
* Preventing gaps.
* Preventing overlaps.
* Updating neighbouring category boundaries when required.
* Re-categorising existing vehicles after category changes.
* Preventing invalid category deletion.

## Repositories

Repositories handle data access through Entity Framework Core.

This keeps database-specific operations out of the controllers and reduces coupling between the UI and data-access code.

## ViewModels

Strongly typed ViewModels are used for form and page data.

This avoids relying on loosely typed `ViewBag` or magic strings for the main application data.



# 7. Database Design

The main entities are:


Manufacturer
Vehicle
VehicleCategory


## Manufacturer

Stores manufacturer information.

Important fields include:

* Id
* Name
* IsDefault
* IsDeleted
* CreatedAt
* UpdatedAt

A unique manufacturer name is enforced.

Default manufacturers cannot be deleted.

A manufacturer that is currently associated with vehicles cannot be deleted.

## Vehicle

Stores vehicle information.

Important fields include:

* Id
* OwnerName
* ManufacturerId
* CategoryId
* YearOfManufacture
* Weight
* IsDeleted
* CreatedAt
* UpdatedAt

Vehicles have relationships with both manufacturers and vehicle categories.

## VehicleCategory

Stores the configurable vehicle weight ranges.

Important fields include:

* Id
* Name
* MinWeight
* MaxWeight
* Icon
* IsDeleted
* CreatedAt
* UpdatedAt

The category ranges are stored in the database rather than hard-coded in the vehicle model.

This means the category configuration can be changed without changing the vehicle entity.



# 8. Category Calculation

Vehicle categories are determined from the vehicle weight.

The application uses the following rule:


MinWeight <= VehicleWeight < MaxWeight


For a category without a maximum weight, the range continues indefinitely.

For example:


Light   0 <= weight < 500
Medium  500 <= weight < 2500
Heavy   2500 <= weight


Therefore:


499.99 kg -> Light
500 kg    -> Medium
2499.99   -> Medium
2500 kg   -> Heavy
2700 kg   -> Heavy


This makes the category boundaries unambiguous.



# 9. Category Boundary Rules

The category configuration must cover the weight range continuously.

The application prevents:

### Gaps

For example:


Light   0 - 500
Medium  600 - 2500


is invalid because the range:


500 - 600


is not covered.

### Overlaps

For example:


Light   0 - 600
Medium  500 - 2500


is invalid because:


500 - 600


belongs to two categories.

### Invalid ranges

A category such as:


3000 - 2000


is rejected.

A category with the same minimum and maximum is also rejected.

### Lowest category

The first category must start at:


0 kg


### Highest category

The final category must cover all remaining weights. In the normal configuration this is represented by a `null` maximum weight.



# 10. Category Changes and Existing Vehicles

Changing a category range can affect vehicles that already exist in the database.

The application therefore recalculates existing vehicle categories after a valid category configuration change.

For example:

### Before


Light   0 - 500
Medium  500 - 2500
Heavy   2500+


A vehicle weighing:


2700 kg


belongs to:


Heavy


If Medium is changed to:


500 - 3000


the resulting configuration becomes:


Light   0 - 500
Medium  500 - 3000
Heavy   3000+


The existing 2700 kg vehicle is automatically changed from:


Heavy


to:


Medium


This behaviour is covered by integration testing through the MVC HTTP request rather than calling the service directly.

This is important because it verifies the complete application flow:


HTTP Request
    ↓
Controller
    ↓
Service
    ↓
Repository
    ↓
SQLite Database




# 11. Vehicle Validation

Vehicle input is validated at the application level.

Examples include:

* Owner name is required.
* Owner name has a maximum length.
* Manufacturer must exist.
* Vehicle category must exist.
* Weight must be greater than zero.
* Year of manufacture must be within the supported range.
* A valid category must exist for the vehicle weight.

The vehicle category is calculated from the configured weight ranges rather than being manually selected by the user.



# 12. Searching, Sorting and Pagination

The vehicle list supports:

* Searching.
* Sorting.
* Pagination.

Sorting is performed on the vehicle list where appropriate, including fields such as:

* Owner
* Weight
* Year
* Manufacturer
* Category

The manufacturer list also supports searching and pagination.

The sorting and filtering logic is kept in the service/repository layer rather than implemented only in the Razor view.



# 13. Delete Behaviour

Vehicles and manufacturers use soft deletion.

Instead of physically removing a record, the application sets:


IsDeleted = true


Deleted records are excluded from normal application queries.

Vehicle categories have additional rules because deleting a category can affect the validity of the entire category configuration.

A category cannot be deleted when:

* It does not exist.
* Vehicles are still associated with it.
* It is the last remaining category.

When a category is deleted, the neighbouring category can inherit the deleted range so that the configuration does not leave an uncovered gap.



# 14. Error Handling

The application handles expected errors without exposing technical details to the user.

Examples include:

* Invalid user input.
* Invalid category ranges.
* Duplicate names.
* Requests for records that do not exist.
* Invalid category deletion.
* Database update failures.

Database update exceptions such as `DbUpdateException` are handled at the controller boundary and converted into friendly messages.

The application does not intentionally expose:


SQL statements
DbUpdateException details
SQLite exceptions
Stack traces


to normal users.

For example, a database failure is presented as a general message such as:


The operation could not be completed. Please try again.


The purpose is to give the user useful feedback without exposing implementation details.



# 15. Automated Testing

The solution contains a separate test project:


VehicleManagement.Tests


The tests are divided into:


VehicleManagement.Tests
│
├── Unit
│   ├── VehicleServiceTests.cs
│   ├── VehicleCategoryServiceTests.cs
│   └── ManufacturerServiceTests.cs
│
└── Integration
    ├── IntegrationTestBase.cs
    ├── VehicleApiFactory.cs
    ├── TestDatabase.cs
    ├── VehicleIntegrationTests.cs
    ├── VehicleCategoryIntegrationTests.cs
    └── ManufacturerIntegrationTests.cs


Unit tests use mocks to test service behaviour in isolation.

Integration tests use:


WebApplicationFactory
HttpClient
SQLite in-memory database


This allows important behaviour to be tested across the application layers.



# 16. Running Automated Tests

From the solution root:

powershell
dotnet test


Or:

powershell
dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj


To run only unit tests:

powershell
dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --filter "FullyQualifiedName~Unit"


To run only integration tests:

powershell
dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --filter "FullyQualifiedName~Integration"


To list all tests:

powershell
dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --list-tests




# 17. What the Tests Focus On

The tests focus on behaviour where an error would have a meaningful effect.

Important areas include:

### Vehicle behaviour

* Vehicle creation.
* Vehicle validation.
* Category calculation.
* Category boundary values.
* Vehicle updates.
* Recalculation after weight changes.
* Vehicle deletion.
* Missing vehicles.

### Category behaviour

* Valid category creation.
* Invalid ranges.
* Range gaps.
* Range overlaps.
* Boundary values.
* Category updates.
* Existing vehicle re-categorisation.
* Invalid category deletion.
* Category continuity.

### Manufacturer behaviour

* Manufacturer creation.
* Duplicate manufacturer names.
* Manufacturer updates.
* Missing manufacturers.
* Default manufacturer protection.
* Manufacturer deletion when vehicles are associated.

Unit tests focus mainly on business logic.

Integration tests verify that the important behaviour works through the actual MVC application and database.



# 18. Design Notes

The main design decision was to keep business rules out of the controllers.

For example, category validation and vehicle re-categorisation are handled by `VehicleCategoryService`.

The controller is responsible mainly for:

* Receiving the request.
* Checking ModelState.
* Calling the service.
* Returning the appropriate view or redirect.
* Showing a user-friendly error message.

This makes the business rules easier to test independently.

Another important decision was to keep vehicle categories database-driven.

Instead of writing:

csharp
if (weight < 500)


directly into the vehicle logic, the application reads the configured category ranges from the database.

This makes the category rules configurable and allows the application to handle changes without modifying the vehicle model.



# 19. Important Assumptions

The implementation assumes:

* Vehicle weights are non-negative and must be greater than zero.
* Category ranges are continuous.
* The first active category starts at 0 kg.
* The final active category has no upper limit.
* Category boundaries use an inclusive minimum and exclusive maximum.
* Category names should be unique.
* Vehicles must always belong to a valid category.
* Deleted records should normally not appear in application lists.
* Default manufacturers are protected from deletion.



# 20. Known Limitations

This is a take-home assignment rather than a production system, so there are some areas that could be improved further.

For a production system I would consider:

* Structured application logging with correlation IDs.
* Global exception handling middleware.
* More detailed audit logging for category changes.
* Authentication and authorization.
* Role-based access control.
* More comprehensive API/end-to-end monitoring.
* CI/CD pipeline integration.
* Database concurrency handling for simultaneous category updates.
* More extensive performance testing with larger datasets.
* Centralised configuration and secret management.
* More extensive automated testing around database failure scenarios.

The current implementation focuses on the assignment requirements and on keeping the core business rules reliable and testable.



# 21. Build and Test Before Submission

Before submitting the application, I use:

powershell
dotnet restore
dotnet build
dotnet test


The expected result is:


Build succeeded


and all automated tests should pass.



## Repository

GitHub repository:

https://github.com/jayabalasubramaniam95-bit/VehicleWebApplication
