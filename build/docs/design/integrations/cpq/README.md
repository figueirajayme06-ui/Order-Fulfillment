# CPQ

The CPQ tables are required within the new OF to find available serialized and non serialized stock, we needed these as tables in our DB so we could join on our assets tables. This also, runs as a container on an Azure Container Environment as a container job running on a schedule as defined in the [Terraform used to provision our resources](/build/infra/).

![High level design](/build/docs/content/.attachments/processes/activation/Container%20Job_%20CPQ.png "CPQ CRON Container job")

# Why move all the CPQ data to new OF?
The long and short of this is we do not want to create an entire new Product Catalogue for OF, so have left the existing application, integrations and DB available for now. We have replicated the schema exactly as is in the existing FAM db, and hopefully at some point we can simply move the connection strings to the new OF DB and disable this job. 

At some point in time in the future a new system will be built for the product catalogue and this work can be replaced.