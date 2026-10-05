# Quotes

The quote code is basically a one time pull on a schedule of quotes that are at 90% chance of completion. They are never updated, but will turn to an agreement when the related Quote is converted to an Order. This also is ran as part of a container job on Azure Container Apps.

![High level design](/build/docs/content/.attachments/processes/activation/Container%20Job_%20Quotes.png "Quotes CRON Container job")

# This looks familiar?
Thats bacause it is blatantly just stolen from the old OF, almost line for line using [this SOQL](src/OF.Common/Infrastructure/IPG/SOQL/get-high-probability-quotes.sql).