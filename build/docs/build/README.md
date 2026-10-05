# Build & Release

1. **PR Pipeline**: 
   - This pipeline is associated to all pull requests to master. It runs some build checks and integration tests whenever a pull request is made to ensure that the proposed changes meet certain quality standards before they are merged into the main branch.
   - ![Build Status](https://aggreko.visualstudio.com/Order%20Fulfilment/_apis/build/status%2FPR?branchName=refs%2Fpull%2F6%2Fmerge)

2. **Infra, Api & Web Pipeline**: 
   - This pipeline handles the deployment of infrastructure (terraform), APIs, and web components of the application. It ensures that changes to these components are properly built and deployed to the production environment.
   - ![Build Status](https://aggreko.visualstudio.com/Order%20Fulfilment/_apis/build/status%2FDeploy%20-%20Infra%20%26%20Web?branchName=master)

3. **Database Pipeline**: 
   - This pipeline is responsible for deploying changes to the SQL database schema or data, it uses visual studio Database projects (No EF core Migrations). It ensures that database changes are applied correctly and efficiently to the production environment.
   - ![Build Status](https://aggreko.visualstudio.com/Order%20Fulfilment/_apis/build/status%2FDeploy%20-%20Database?branchName=master)

4. **Assets Pipeline**: 
   - This pipeline handles the deployment of the container job to pull assets from the ION data lake.
   - ![Build Status](https://aggreko.visualstudio.com/Order%20Fulfilment/_apis/build/status%2FDeploy%20-%20Data%20-%20Assets?branchName=master)

5. **Products Pipeline**: 
   - This pipeline probably deals with deploying updates or changes to Non serialized products from the ION Data Lake. It ensures that product information is up-to-date and correctly integrated into the application.
   - ![Build Status](https://aggreko.visualstudio.com/Order%20Fulfilment/_apis/build/status%2FDeploy%20-%20Data%20-%20Product%20Items?branchName=master)

6. **Quotes Pipeline**: 
   - This pipeline handles the deployment of Quotes from Salesforce. It ensures that quotes are generated and managed correctly within the application.
   - ![Build Status](https://aggreko.visualstudio.com/Order%20Fulfilment/_apis/build/status%2FDeploy%20-%20Data%20-%20Quotes?branchName=master)

7. **CPQ Pipeline**: 
   - This pipeline is associated with syncing the customer, pricing, and quoting (CPQ) processes data from the old OF to the new one. It ensures that CPQ functionality is properly deployed and operational.
   - ![Build Status](https://aggreko.visualstudio.com/Order%20Fulfilment/_apis/build/status%2FDeploy%20-%20Data%20-%20CPQ?branchName=master)

8. **CodeQL Pipeline**: 
   - This pipeline is related to security analysis using CodeQL. It checks the codebase for potential security vulnerabilities, ensuring that the application code meets security standards.
   - ![CodeQL](https://github.com/AggrekoTechnologyServices/Order-Fulfillment/actions/workflows/codeql.yml/badge.svg)


## Version numbers

Version numbers for each of these pipelines are managed manually via the [variables.yaml](/.azd/actions/variables.yml) and sets the container version for the integration jobs and also the version for the Infra, Api & web applications.