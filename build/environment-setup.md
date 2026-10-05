# Setting up a new Environment

Thisa guide will walk through how to setup a new NOF environment

## 1. Make an App Registration for your environment

You can make an app registration using the [infra-auth repository](https://github.com/AggrekoTechnologyServices/infra-auth/) here and using the [IPG pipeline](https://github.com/AggrekoTechnologyServices/infra-auth/actions/workflows/ipg.yml). An example can be found [here of an app registration being made](https://github.com/AggrekoTechnologyServices/infra-auth/blob/main/terraform/ipg/main.tf).

## 2. Settings (DevOps)

You will also need to make a new ['environment' with approval steps in Devops](https://dev.azure.com/aggreko/Order%20Fulfilment/_environments).

Clone to make a new ['library' variable group in DevOps](https://dev.azure.com/aggreko/Order%20Fulfilment/_library?itemType=VariableGroups).

## 3. Update the pipeline with new environment step

All the Devops pipelines will require new steps to be created for your environment in the ['workflows' directory](/.azd/workflows).

## 4. Run the pipeline for container jobs

- [Deploy - Data - CPQ](https://dev.azure.com/aggreko/Order%20Fulfilment/_build?definitionId=671)
- [Deploy - Data - Warehouses](https://dev.azure.com/aggreko/Order%20Fulfilment/_build?definitionId=677)
- [Deploy - Data - Assets](https://dev.azure.com/aggreko/Order%20Fulfilment/_build?definitionId=667)
- [Deploy - Data - Product Items](https://dev.azure.com/aggreko/Order%20Fulfilment/_build?definitionId=670)
- [Deploy - Data - Quotes](https://dev.azure.com/aggreko/Order%20Fulfilment/_build?definitionId=669)

## 5. Run the Infra pipeline

- [Infra pipeline](https://dev.azure.com/aggreko/Order%20Fulfilment/_build?definitionId=788)

## 6. Settings (KeyVault)

Update the Keyvaults in the ['build/config/move-config.py'](/build/config/move-config.py) and run it after you have done an az login and created a vnenv to move the settings.

```
python -m venv venv
venv\Scripts\activate
pip install -r .\requirements.txt
```

You'll likely want to update any configurations to the new enviornment settings so as to avoid duplicate message processing.

## 7. Run the Infra and Web pipeline

This deploys the site and the api, so should be good.