from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient

# Define your source and destination Key Vault URLs
source_keyvault_url = "https://kv-ofdev.vault.azure.net/"
destination_keyvault_url = "https://kv-ofsit.vault.azure.net/"

# Create Key Vault clients for both source and destination Key Vaults
credential = DefaultAzureCredential()
source_client = SecretClient(vault_url=source_keyvault_url, credential=credential)
destination_client = SecretClient(vault_url=destination_keyvault_url, credential=credential)

# List all secrets in the source Key Vault
source_secrets = source_client.list_properties_of_secrets()

# Iterate through the secrets in the source Key Vault
for secret in source_secrets:
    # Check if the secret already exists in the destination Key Vault
    try:
        destination_client.get_secret(secret.name)
        print(f"Secret '{secret.name}' already exists in the destination Key Vault. Skipping.")
    except:
        # If the secret doesn't exist in the destination Key Vault, copy it
        secret_value = source_client.get_secret(secret.name).value
        destination_client.set_secret(secret.name, secret_value)
        print(f"Copied secret '{secret.name}' to the destination Key Vault.")

print("Key Vault copy completed.")
