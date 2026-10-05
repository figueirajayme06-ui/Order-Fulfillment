# Solution

The solution is broken in to 4 sections distinct areas across 3 solution folders.

* Orchestration - _Background data jobs._
* Presentation - _The Function API (and message processing) and UI (also contains APIs relating to UI) projects._
* Tests - _Integration and Unit tests._

The rest of the projects are related to database projects and shared libraries. The only real prerequisite setup steps are.

* Visual Studio 2022
* [EF Core Power Tools to reverse engineer DB](https://marketplace.visualstudio.com/items?itemName=ErikEJ.EFCorePowerTools)


## Orchestration

Orchestration jobs on Azure Container Jobs which are PaaS short lived container jobs which essentially scale to 0. All jobs can be ran locally without the need for Docker.

* [OF.Data.Assets](/build/docs/design/integrations/assets/README.md)
* [OF.Data.CPQ](/build/docs/design/integrations/cpq/README.md)
* [OF.Data.ProductItems](/build/docs/design/integrations/product-items/README.md)
* [OF.Data.Quotes](/build/docs/design/integrations/quotes/README.md)
* [OF.Data.Warehouses](/build/docs/design/integrations/warehouses/README.md)

## Presentation

The presentation layer of the application comprises of an Azure function project using dotnet core and a ASP.Net Core WebApp running on an Azure App Service.

#### Function Api

A [swagger document can be found for the function API](https://faofdev.azurewebsites.net/api/swagger/ui) which details the only API which is connected too externally from ION.

The rest of the functions are all message bus processors used for external [Agreement and Line processing](/build/docs/design/integrations/agreement/README.md).

#### UI

The UI is a standard ASP.Net app using easy auth via an app service. The app uses the [Infragistics libraries](https://www.infragistics.com) to build the tables along with a lot of custom JavaScript.

This is an area of high complexity and should tread carefully in this area

## Tests

Basic running of tests, integration and unit which hopefully cover end to end the data integration scenarios. [More details of the testing can be found here](/src/OF.Tests/README.md). These are only ran via the [PR devops pipeline](/.azd/workflows/pr.yml).

## Database

The database is a MSSql database ran locally on LocalDB and on the deployed application on Azure Sql. The management of the database is done via a Data project where all, scripts, triggers, procedures and views are managed. These can be deployed via Visual Studio to LocalDB and also via the [build and release pipeline specifically for the database](/.azd/workflows/deploy-sql.yml).

#### EF Core

The EF core process can be done using the [EF Core Power Tools to reverse engineer DB](https://marketplace.visualstudio.com/items?itemName=ErikEJ.EFCorePowerTools). This takes the DB changes you've applied via Database project and generate the data layer for you. 

For some reason this also modifies a couple of non-EF files, not figured out why yet, but you can just undo the changes on that :D.