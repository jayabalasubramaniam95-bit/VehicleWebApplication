# Vehicle Management - Automated Tests

This project contains the automated tests for the Vehicle Management application.

The test project is separated into **unit tests** and **integration tests**.

The intention is not to test every small implementation detail. The tests focus on the application behaviour where defects could have meaningful consequences, especially vehicle category calculation, category range validation and existing vehicle re-categorisation.



# 1. Testing Approach

The test project uses two types of tests:

```text
Unit Tests
    ↓
Business logic in isolation

Integration Tests
    ↓
HTTP Request
    ↓
Controller
    ↓
Service
    ↓
Repository
    ↓
EF Core
    ↓
SQLite Database
```

## Unit Tests

Unit tests test individual services without running the complete ASP.NET Core application.

Mocks are used for repository dependencies.

The unit tests are useful for testing business rules quickly and in isolation.

## Integration Tests

Integration tests verify important application behaviour through the actual MVC application.

They use:

* `WebApplicationFactory`
* `HttpClient`
* Entity Framework Core
* SQLite in-memory database
* xUnit

This allows the tests to verify that the different application layers work together.



# 2. Test Project Structure

```text
VehicleManagement.Tests
│
├── Unit
│   ├── VehicleServiceTests.cs
│   ├── VehicleCategoryServiceTests.cs
│   └── ManufacturerServiceTests.cs
│
├── Integration
│   ├── IntegrationTestBase.cs
│   ├── VehicleApiFactory.cs
│   ├── TestDatabase.cs
│   ├── VehicleIntegrationTests.cs
│   ├── VehicleCategoryIntegrationTests.cs
│   └── ManufacturerIntegrationTests.cs
│
└── README.md
```



# 3. Unit Tests

## VehicleServiceTests

`VehicleServiceTests.cs` focuses on vehicle business logic.

Important behaviours include:

* Vehicle creation.
* Vehicle validation.
* Manufacturer validation.
* Vehicle category determination.
* Invalid vehicle data.
* Vehicle update behaviour.
* Vehicle deletion behaviour.
* Missing vehicle handling.

Repository dependencies are mocked using Moq.

This keeps the tests focused on the service logic rather than the database.



# 4. VehicleCategoryServiceTests

`VehicleCategoryServiceTests.cs` focuses on the most important business rules in the assignment.

The tests cover:

* Category calculation.
* Category boundaries.
* Valid category creation.
* Invalid category ranges.
* Range gaps.
* Range overlaps.
* Category updates.
* Existing vehicle re-categorisation.
* Invalid category deletion.
* Category continuity.

For example, the boundary behaviour is based on:

```text
Light   0 <= weight < 500
Medium  500 <= weight < 2500
Heavy   2500 <= weight
```

Therefore:

```text
499.99 kg -> Light
500 kg    -> Medium
2499.99   -> Medium
2500 kg   -> Heavy
```

The category service tests are particularly important because category configuration affects existing vehicle records.



# 5. ManufacturerServiceTests

`ManufacturerServiceTests.cs` covers manufacturer business rules.

Important behaviours include:

* Creating manufacturers.
* Updating manufacturers.
* Duplicate manufacturer validation.
* Missing manufacturer handling.
* Default manufacturer protection.
* Preventing deletion when vehicles are associated.
* Manufacturer deletion behaviour.



# 6. Integration Tests

The integration tests verify the application through the real MVC request pipeline.

The general flow is:

```text
xUnit
  ↓
HttpClient
  ↓
ASP.NET Core MVC
  ↓
Controller
  ↓
Service
  ↓
Repository
  ↓
Entity Framework Core
  ↓
SQLite in-memory database
```

This gives better confidence than testing the controller or service alone.



# 7. Integration Test Database

The integration tests use SQLite in-memory rather than the developer's SQL Server database.

This means the tests:

* Do not modify the development database.
* Do not require the test machine's SQL Server database.
* Start with a controlled database.
* Can run repeatedly.
* Are suitable for automated test execution.

The test database is created and initialised by the test infrastructure.

The application itself continues to use SQL Server during normal development.



# 8. Vehicle Integration Tests

`VehicleIntegrationTests.cs` covers important vehicle workflows.

The tests include:

### Vehicle pages

* Vehicle list.
* Vehicle create page.
* Vehicle details.
* Vehicle edit page.

### Vehicle creation

* Valid vehicle creation.
* Invalid manufacturer.
* Invalid weight.
* Category assignment.
* Light category.
* Medium category.
* Heavy category.
* Category boundary values.

### Vehicle update

* Updating vehicle information.
* Recalculating the category when the vehicle weight changes.
* Attempting to update a vehicle that does not exist.

### Vehicle deletion

* Soft deleting a vehicle.
* Attempting to delete a vehicle that does not exist.



# 9. Vehicle Category Integration Tests

`VehicleCategoryIntegrationTests.cs` covers the category behaviour through HTTP.

Important scenarios include:

### Category pages

* Category list.
* Category create.
* Category edit.
* Missing category.

### Invalid configuration

* Invalid ranges.
* Reverse ranges.
* Equal minimum and maximum.
* Gaps between ranges.
* Overlapping ranges.

### Existing vehicle re-categorisation

One of the most important integration tests verifies the assignment requirement:

```text
Before:

Light   0 - 500
Medium  500 - 2500
Heavy   2500+

Vehicle weight = 2700
Vehicle category = Heavy
```

The test then updates Medium:

```text
Medium = 500 - 3000
```

The resulting configuration becomes:

```text
Light   0 - 500
Medium  500 - 3000
Heavy   3000+
```

The existing 2700 kg vehicle must then be assigned to:

```text
Medium
```

The test performs this through the HTTP/controller layer rather than directly calling:

```csharp
VehicleCategoryService.Update(...)
```

This is intentional because it verifies the complete application flow.



# 10. Category Boundary Testing

Category boundaries are important because an incorrect `<` or `<=` comparison can cause a vehicle to be assigned incorrectly.

The application uses:

```text
MinWeight <= Weight < MaxWeight
```

For the default configuration:

```text
500 kg  -> Medium
2500 kg -> Heavy
```

The integration tests verify these boundary values.



# 11. Category Gap and Overlap Testing

The category configuration must not contain gaps.

For example:

```text
Light   0 - 500
Medium  600 - 2500
```

is rejected.

The configuration must also not contain overlaps.

For example:

```text
Light   0 - 600
Medium  500 - 2500
```

is rejected.

These tests are important because an invalid category configuration could result in vehicles that cannot be categorised or vehicles that could match more than one category.



# 12. Category Deletion Testing

Category deletion is also tested because removing a category can make the entire configuration invalid.

The tests cover:

* Category does not exist.
* Category contains vehicles.
* Last category protection.
* Successful deletion where permitted.
* Maintaining category continuity.

The application must not allow a category deletion to leave an invalid range configuration.



# 13. Manufacturer Integration Tests

`ManufacturerIntegrationTests.cs` covers the main manufacturer workflows.

The tests include:

* Manufacturer list.
* Manufacturer create page.
* Manufacturer details.
* Missing manufacturer.
* Manufacturer edit.
* Missing manufacturer edit.
* Manufacturer deletion.
* Default manufacturer protection.
* Manufacturer deletion when vehicles are associated.



# 14. HTTP and MVC Testing

Integration tests use `HttpClient`.

For normal GET requests:

```text
200 OK
```

is expected when the requested page exists.

For missing records:

```text
404 Not Found
```

is expected where appropriate.

For successful form submissions:

```text
302 Redirect
```

is normally expected.

Automatic redirects are disabled when the test needs to inspect the original response:

```csharp
using var client = Factory.CreateClient(
    new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });
```



# 15. Anti-Forgery Testing

The application uses ASP.NET Core anti-forgery protection for POST requests.

The integration tests therefore:

1. Request the form page.
2. Read the anti-forgery token from the HTML.
3. Include the token in the POST request.
4. Check the returned response.

This makes the tests closer to how the real browser application behaves.



# 16. Error Handling Tests

The automated tests also cover expected error behaviour.

Examples include:

* Invalid user input.
* Invalid category configuration.
* Missing records.
* Invalid category deletion.
* Invalid manufacturer operations.

The application should return a useful user-facing error rather than exposing technical implementation details.

The expected behaviour is to avoid exposing information such as:

```text
SQL statements
DbUpdateException
SQLite exceptions
Stack traces
```

to normal users.



# 17. Running All Tests

From the solution root:

```powershell
dotnet test
```

or:

```powershell
dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj
```



# 18. Running Unit Tests Only

```powershell
dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --filter "FullyQualifiedName~Unit"
```



# 19. Running Integration Tests Only

```powershell
dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --filter "FullyQualifiedName~Integration"
```



# 20. Running Individual Test Classes

For vehicle integration tests:

```powershell
dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --filter "FullyQualifiedName~VehicleIntegrationTests"
```

For vehicle category integration tests:

```powershell
dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --filter "FullyQualifiedName~VehicleCategoryIntegrationTests"
```

For manufacturer integration tests:

```powershell
dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --filter "FullyQualifiedName~ManufacturerIntegrationTests"
```

For a specific test:

```powershell
dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --filter "FullyQualifiedName~UpdateCategory_WhenRangeChanges_RecategorisesExistingVehicles"
```



# 21. List All Tests

To see the available tests:

```powershell
dotnet test .\VehicleManagement.Tests\VehicleManagement.Tests.csproj --list-tests
```



# 22. Test Philosophy

The tests are intentionally focused on behaviour rather than implementation details.

For example, the tests do not try to verify every private method or every trivial property assignment.

Instead, they focus on behaviours where a defect could cause an incorrect result for a user.

The highest priority areas are:

```text
Category calculation
Category boundaries
Range gaps
Range overlaps
Existing vehicle re-categorisation
Vehicle validation
Manufacturer validation
Delete restrictions
Missing records
```

This follows the assignment requirement to focus testing on meaningful behaviour.



# 23. Unit vs Integration Tests

The two test types have different purposes.

## Unit tests

Use unit tests when the business logic can be tested independently.

Advantages:

* Fast.
* Isolated.
* Easy to identify the failing service rule.
* Repository dependencies can be mocked.

## Integration tests

Use integration tests when the behaviour depends on several application layers working together.

Advantages:

* Tests the real MVC request pipeline.
* Tests controllers and services together.
* Tests repositories and EF Core.
* Tests database behaviour.
* Provides confidence that the complete workflow works.

The category re-categorisation scenario is intentionally tested as an integration test because the important requirement is that an HTTP category update ultimately changes the existing vehicle's database record.



# 24. Before Submission

From the solution root I run:

```powershell
dotnet restore
dotnet build
dotnet test
```

The expected result is:

```text
Build succeeded
```

and:

```text
Failed: 0
```

All tests should pass before submitting the assignment.



# 25. Summary

The test suite is designed around the main business risks in the Vehicle Management application.

The most important behaviours tested are:

* Preventing category gaps.
* Preventing category overlaps.
* Correct category boundary handling.
* Re-categorising existing vehicles when category ranges change.
* Vehicle validation.
* Manufacturer validation.
* Safe deletion.
* Missing record handling.
* Expected error handling.
* Sorting and filtering behaviour where applicable.

The combination of unit and integration tests provides coverage of both isolated business rules and the complete application workflow.
