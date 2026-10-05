# Product Items

The Non serialized product info is required within the new OF to allow users to select from products in M3. This also, runs as a container on an Azure Container Environment as a container job running on a schedule as defined in the [Terraform used to provision our resources](/build/infra/).

![High level design](/build/docs/content/.attachments/processes/activation/Container%20Job_%20Products.png "Products CRON Container job")

# Additional complexities
The product details are pulled as you can see above from the container job running this [SQL against the ION data lake](/src/OF.Common/Infrastructure/CloudSuite/SQL/get-product_details.sql), but there are also a couple of other ways we keep this data updated.
