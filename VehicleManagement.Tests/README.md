# Vehicle Management - Automated Integration Tests

This project contains the automated integration tests for the Vehicle Management application.

The tests send HTTP requests to the ASP.NET Core application and check the actual application behaviour instead of testing controllers separately.

## Testing Technology

* .NET 10
* xUnit
* ASP.NET Core `WebApplicationFactory`
* `HttpClient`
* Entity Framework Core
* SQLite in-memory database

## How the Tests Work

The test application is created using:

WebApplicationFactory<Program>

The tests use `HttpClient` to send requests to the application.

The main flow is:

xUnit Test
    ↓
HttpClient
    ↓
ASP.NET Core Application
    ↓
Controller
    ↓
Service
    ↓
Repository
    ↓
SQLite In-Memory Database

This allows me to test the application across multiple layers.

## Test Database

The integration tests use a SQLite in-memory database instead of the local SQL Server database.

This means the tests:

* Use a separate test database
* Do not change the development database
* Do not require a local SQL Server database
* Start with a clean database
* Run faster

The database is created for the test environment.

## Vehicle Tests

The vehicle integration tests cover the main vehicle workflows.

### Vehicle List

The tests check that the vehicle list page returns a successful response.

### Vehicle Create

The tests cover:

* Creating a vehicle with valid data
* Invalid manufacturer
* Weight that does not match a category
* Light category assignment
* Medium category assignment
* Heavy category assignment
* Category boundary values

### Vehicle Edit

The tests cover:

* Editing an existing vehicle
* Updating vehicle information
* Recalculating the category when the weight changes
* Trying to edit a vehicle that does not exist

### Vehicle Delete

The tests cover:

* Soft deleting a vehicle
* Checking that the vehicle is marked as deleted
* Trying to delete a vehicle that does not exist

## Manufacturer Tests

The manufacturer integration tests cover:

* Manufacturer list
* Manufacturer create page
* Manufacturer details
* Details for a manufacturer that does not exist
* Manufacturer edit
* Edit for a manufacturer that does not exist
* Manufacturer deletion behaviour

## HTTP Response Testing

The tests check the HTTP responses returned by the application.

For example:

200 OK
302 Redirect
404 Not Found

For tests where I need to check a redirect, automatic redirects are disabled:

using var client = Factory.CreateClient(
    new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

This allows the test to check the original response from the application.

## Anti-Forgery Token

The application uses ASP.NET Core anti-forgery protection for POST requests.

The integration tests get the anti-forgery token before submitting POST requests and send the token with the request.

This means the tests use the same anti-forgery protection as the real application.

## Running the Tests

From the solution root:

dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj

## Run Integration Tests

dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --filter "FullyQualifiedName~Integration"

## Run Vehicle Tests

dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --filter "FullyQualifiedName~VehicleIntegrationTests"

## Run Manufacturer Tests

dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --filter "FullyQualifiedName~ManufacturerIntegrationTests"

## Run One Test

For example:

dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --filter "FullyQualifiedName~CreateVehicle"

## List Tests

To see all available tests:

dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --list-tests

## Test Result

The complete test suite has been run successfully.

The expected result is:

Failed: 0

The tests cover both successful scenarios and validation/error scenarios.

## Why I Used Integration Tests

I used integration tests because important parts of this application work together across different layers.

For example, creating a vehicle goes through:

HTTP POST
   ↓
Vehicle Controller
   ↓
Vehicle Service
   ↓
Vehicle Repository
   ↓
Entity Framework Core
   ↓
SQLite Database

Testing this complete flow gives better confidence that the different parts of the application work together correctly.

## Test Project Structure

VehicleManagement.Tests
│
├── Integration
│   ├── IntegrationTestBase.cs
│   ├── VehicleApiFactory.cs
│   ├── TestDatabase.cs
│   ├── VehicleIntegrationTests.cs
│   └── ManufacturerIntegrationTests.cs
│
└── README.md

## Final Check Before Submission

I use the following commands before submitting the project:

dotnet build

Then:

dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj

Both commands should complete successfully.
