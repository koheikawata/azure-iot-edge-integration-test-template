# Getting Started
# IaC pipeline

## Set up Azure Service Connection
- Azure DevOps -> Project settings -> Service connections -> New service connection
   - Azure Resource Manager -> Service Principal (automatic)
     - Scope level: Subscription
     - Select Subscription
     - Resource Group can be empty (Select a resource group if you want to scope)
     - Define a service connection name
     - Save

## Set up variables
### variables.yml

- Set up [pipelines/templates/variables.yml](../pipelines/templates/variables.yml)

| Name | Description |
| -------- | ---------- |
| BASE_NAME | Azure resource base name. ex. rg-{BASE_NAME} |
| LOCATION | Azure resource location |

**`BASE_NAME` should be globally unique, otherwise it could cause a conflict error**

### Pipeline Library variable group

- Create a pipeline library variable group named `vg-edge-test` and set up it following [Library of assets](https://docs.microsoft.com/en-us/azure/devops/pipelines/library)

| Name | Description |
| -------- | ---------- |
| AZURE_SVC_NAME | Name of Azure Service Connection created  |
| VM_SVC_NAME    | Name of SSH Service Connection to access Edge VM (created later)  |

## Azure Pipelines

- Azure DevOps -> Pipelines -> New pipeline
  - Select your repository type. ex. Azure Repos Git
  - Select your repository name. ex. EdgeIntegrationTest
  - Select "Existing Azure Pipelines YAML file".
  - Path: `/pipelines/iac.yml` and "Continue".
  - Variables -> New variable
    - Name: `VM_PASS`
    - Value: **Define your VM login password**
    - Check "Keep this value secret"
  - Run the pipeline

**Azure VM password should follow the strict policy below**

[What are the password requirements when creating a VM?](https://docs.microsoft.com/en-us/azure/virtual-machines/linux/faq#what-are-the-password-requirements-when-creating-a-vm-)

# Set up IoT Edge

## Create SSH Service Connection

- Azure DevOps -> Project settings -> Service connections -> New service connection
   - SSH -> Next
     - Host name: `edge-{BASE_NAME}.japaneast.cloudapp.azure.com`
     - Port number: 22
     - Username: `testuser`
     - Password: The one defined in the previous step
     - Service connection name: The one defined in Pipeline Library variable group
     - Save

## Grant Blob Contributor role to Azure Pipeline agent

Azure Pipelines agent is required to have `Storage Blob Data Contributor` to generate Blob SAS token to pass to `FileGenerator` module.

- Extract Azure Active Directory Object ID of Azure Service Connection
  - Azure Portal -> Azure Active Directory -> App registrations -> Find the Azure Service Connection app
  - Overview -> Managed application in local directory (Redirect to Enterprise Application section)
    - Keep Object ID for later usage
- Azure CLI logged in your subscription with Powershell
```
az role assignment create `
    --role "Storage Blob Data Contributor" `
    --assignee {Object ID of Azure Service Connection} `
    --scope "/subscriptions/{Azure Subscription ID}/resourceGroups/rg-{BASE_NAME}/providers/Microsoft.Storage/storageAccounts/st{BASE_NAME}"
```

## Create IoT Edge device

- Azure Portal -> Your IoT Hub -> IoT Edge -> + Add IoT Edge Device
  - Device ID: `IntegrationTest`
  - Save
  - Keep **Primary Connection String** for the next step

## Install IoT Edge runtime

- Azure Portal -> Your Virtual Machine
  - Start if not yet
- Go to a terminal of your local machine
  - Log in to the virtual machine
    - Name: `testuser`, defined at [main.parameters.json](../src/bicep/main.parameters.json)
    - Password: The value of `VM_PASS` you defined in the previous step
      - Example command: `ssh testuser@edge-{BASE_NAME}.japaneast.cloudapp.azure.com`
  - Install IoT Edge runtime and set up configurations
    - Follow this instruction of Microsoft Documentation - [Install IoT Edge](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-provision-single-device-linux-symmetric?view=iotedge-2020-11&tabs=azure-portal%2Cubuntu#install-iot-edge)

# Integration test pipeline
## Azure Pipelines

- Azure DevOps -> Pipelines -> New pipeline
  - Select your repository type. ex. Azure Repos Git
  - Select your repository name. ex. EdgeIntegrationTest
  - Select "Existing Azure Pipelines YAML file".
  - Path: `/pipelines/integration-test.yml` and "Continue".
  - Run the pipeline
