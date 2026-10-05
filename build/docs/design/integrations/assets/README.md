# Assets

This job is responsible for grabbing on an interval the current state of the assets in the MITLOC table and its current status and dates. This runs as a container on an Azure Container Environment as a container job running on a schedule as defined in the [Terraform used to provision our resources](/build/infra/README.md).



![High level design](/build/docs/content/.attachments/processes/activation/Container%20Job_%20Assets.png "Asset CRON Container job")

# Additional complexities
The asset tables are pulled as you can see above from the container job running this [SQL against the ION data lake](/src/OF.Common/Infrastructure/CloudSuite/SQL/get-assets.sql), but there are also a couple of other ways we keep this data updated.

There is an [API we created for ION to post the Skeletal assets messages too](/src/OF.Api/AssetMessageFunctions.cs), this way when a change happens in MITLOC we get notified of the data changes and can update the Gantt chart in the application for assets.This is a Data Flow managed inside ION guys and send us the following JSON.

![AssetsApi Json](/build/docs/content/.attachments/json/assets.json)

Though this is good for location changes, when a line changes we do not get notified of the date changes for the asset, so when we [receive a Line Sync BOD](/src/OF.Api/AgreementMessageFunctions.cs) we also update an related asset delivery, termination, collection, valid to and valid from date. 