
param base_name string

@secure()
param vm_pass string

param vnet_address_prefix string
param subnet_address_prefix string
param vm_size string
param os_disk_type string
param ubuntu_os_version string
param vm_username string
param iothub_cg_name string

param location string = resourceGroup().location

var nsg_name = 'nsg-${base_name}'
var vnet_name = 'vnet-${base_name}'
var subnet_name = 'snet-${base_name}'
var public_ip_name = 'pip-${base_name}'
var dns_label = 'edge-${base_name}'
var nic_name = 'nic-${base_name}'
var vm_name = 'ubuntu-${base_name}'
var iothub_name = 'iot-${base_name}'
var acr_name = 'cr${base_name}'
var storage_account_name = 'st${base_name}'

resource NetworkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2021-05-01' = {
  name: nsg_name
  location: location
  properties: {
    securityRules: [
      {
        name: 'SSH'
        properties: {
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '22'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 100
          direction: 'Inbound'
        }
      }
    ]
  }
}

resource VirtualNetwork 'Microsoft.Network/virtualNetworks@2021-05-01' = {
  name: vnet_name
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnet_address_prefix
      ]
    }
    subnets: [
      {
        name: subnet_name
        properties: {
          addressPrefix: subnet_address_prefix
          networkSecurityGroup: {
            id: NetworkSecurityGroup.id
          }
        }
      }
    ]
  }
}

resource PublicIp 'Microsoft.Network/publicIPAddresses@2021-05-01' = {
  name: public_ip_name
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    publicIPAllocationMethod: 'Dynamic'
    publicIPAddressVersion: 'IPv4'
    dnsSettings: {
      domainNameLabel: dns_label
    }
    idleTimeoutInMinutes: 4
  }
}

resource NetworkInterface 'Microsoft.Network/networkInterfaces@2021-05-01' = {
  name: nic_name
  location: location
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          subnet: {
            id: VirtualNetwork.properties.subnets[0].id
          }
          privateIPAllocationMethod: 'Dynamic'
          publicIPAddress: {
            id: PublicIp.id
          }
        }
      }
    ]
    networkSecurityGroup: {
      id: NetworkSecurityGroup.id
    }
  }
}

resource VirtualMachine 'Microsoft.Compute/virtualMachines@2021-11-01' = {
  name: vm_name
  location: location
  properties: {
    hardwareProfile: {
      vmSize: vm_size
    }
    storageProfile: {
      osDisk: {
        createOption: 'FromImage'
        managedDisk: {
          storageAccountType: os_disk_type
        }
      }
      imageReference: {
        publisher: 'Canonical'
        offer: '0001-com-ubuntu-server-focal'
        sku: ubuntu_os_version
        version: 'latest'
      }
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: NetworkInterface.id
        }
      ]
    }
    osProfile: {
      computerName: vm_name
      adminUsername: vm_username
      adminPassword: vm_pass
      linuxConfiguration: null
    }
  }
}

resource IoTHub 'microsoft.devices/iotHubs@2021-07-02' = {
  name: iothub_name
  location: location
  sku: {
    name: 'S1'
    capacity: 1
  }
}

resource IoTHubConsumerGroup 'Microsoft.Devices/IotHubs/eventHubEndpoints/ConsumerGroups@2021-07-02' = {
  name: '${IoTHub.name}/events/${iothub_cg_name}'
  properties:{
    name: iothub_cg_name
  }
}

resource ContainerRegistry 'Microsoft.ContainerRegistry/registries@2021-12-01-preview' = {
  name: acr_name
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    adminUserEnabled: true
  }
}

resource StorageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' = {
  name: storage_account_name
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    isHnsEnabled: true
  }
}
