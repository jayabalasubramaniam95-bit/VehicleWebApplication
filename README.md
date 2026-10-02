# Vehicle Management System

A vehicle management web application built using **ASP.NET Core MVC, C#, Entity Framework Core and SQL Server**.

The application allows users to manage vehicles, manufacturers and vehicle categories. It includes validation, searching, sorting, pagination and soft deletion.

## Technology Used

* .NET 10
* ASP.NET Core MVC
* C#
* Entity Framework Core
* SQL Server
* Razor Views
* HTML / CSS / JavaScript
* jQuery
* Dependency Injection
* xUnit
* SQLite in-memory database for integration testing

## Project Structure

The application follows a simple layered structure:


Views
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


### Controllers

Controllers handle requests from the UI, validation and navigation between pages.

### Services

Services contain the main business logic, such as:

* Vehicle validation
* Vehicle category calculation
* Manufacturer validation
* Soft deletion
* Category range validation

### Repositories

Repositories are responsible for database operations using Entity Framework Core.

### Models

The main entities are:

* Vehicle
* Manufacturer
* VehicleCategory

## Features

### Vehicle Management

* Add a vehicle
* Edit a vehicle
* View vehicle details
* Soft delete a vehicle
* Search vehicles
* Sort vehicles
* Pagination
* Select a manufacturer
* Select vehicle weight
* Automatically assign the vehicle category based on weight

### Manufacturer Management

* Add manufacturer
* Edit manufacturer
* View manufacturer details
* Soft delete manufacturer
* Search manufacturers
* Pagination
* Prevent duplicate manufacturer names
* Default manufacturers cannot be deleted
* Manufacturers currently used by vehicles cannot be deleted

### Vehicle Categories

Vehicle categories are stored in the database.

The category is automatically selected based on the vehicle weight.

If the vehicle weight is changed, the category is recalculated.

The category weight ranges can also be updated through the application.

## Vehicle Category

The category is based on the configured weight ranges.

For example:


Light
0 - 500 kg

Medium
500 - 2500 kg

Heavy
2500 kg and above


These ranges are stored in the `VehicleCategories` table instead of being hard-coded in the vehicle model.

This makes it possible to change the category ranges without changing the vehicle code.

## Database

The application uses **SQL Server** during normal development.

Entity Framework Core migrations are used to create and update the database.

### Create a migration

From the project directory:

powershell
dotnet ef migrations add MigrationName


### Update the database


dotnet ef database update


## Running the Application

From the solution root:


dotnet restore
dotnet build
dotnet run --project .\VehicleManagement\VehicleManagement.csproj


ASP.NET Core will display the application URL in the terminal.

Open that URL in a browser.

## Connection String

The SQL Server connection string is configured in:


VehicleManagement/appsettings.json


Example:


{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=VehicleManagement;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}


The connection string can be changed depending on the local SQL Server setup.

## Database Seeding

The application contains seed data for the default manufacturers and vehicle categories.

Default manufacturers are protected from deletion.

Fixed seed values are used so that Entity Framework Core migrations remain consistent.

## Validation

Validation is handled at different levels of the application.

Some of the validations include:

* Required fields
* Maximum field lengths
* Vehicle manufacturing year
* Vehicle weight must be greater than zero
* Manufacturer must exist
* Vehicle category must exist
* Duplicate manufacturer names
* Valid category weight ranges

Server-side validation is used together with client-side validation where appropriate.

## Soft Delete

Vehicles and manufacturers use soft deletion.

Instead of permanently deleting a record, the application sets:


IsDeleted = true

Deleted records are then excluded from the normal application queries.

## Automated Testing

The automated integration tests are in:

VehicleManagement.Tests

The tests use an isolated **SQLite in-memory database** so they do not depend on the local SQL Server database.

More information about the tests is available here:

VehicleManagement.Tests/README.md

## Build

To build the application:

dotnet build

## Run Tests

To run all automated tests:

dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj

## Project Structure

VehicleManagement
│
├── Controllers
├── Data
├── Models
├── Repositories
├── Services
├── ViewModels
├── Views
├── wwwroot
├── Migrations
├── Program.cs
└── appsettings.json

VehicleManagement.Tests
│
├── Integration
└── README.md

## Design Decisions

I used a **Controller → Service → Repository** structure to keep the different responsibilities separate.

The main business rules are handled in the service layer rather than putting everything inside the controllers.

I used strongly typed Razor ViewModels for the UI instead of relying on loosely typed data.

Entity Framework Core migrations are used to manage database changes.

For integration testing, I used SQLite in-memory so the tests run against an isolated database and do not affect the local SQL Server database.

## Run the Full Solution

The basic commands to restore, build, test and run the application are:

dotnet restore

dotnet build

dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj

dotnet run --project .\VehicleManagement\VehicleManagement.csproj

## Notes

This project was developed as a practical ASP.NET Core MVC application with a focus on clean separation of responsibilities, database-driven business rules, validation and automated integration testing.
