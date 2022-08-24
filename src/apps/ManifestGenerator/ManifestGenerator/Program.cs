using Azure.Storage.Files.DataLake;
using Azure.Storage;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Configuration;
using System.Text.Encodings.Web;
using System.Text.Json;
using ManifestGenerator.Services;

namespace ManifestGenerator;

public class Program
{
    private static readonly string storageAccountName = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME") ?? string.Empty;
    private static readonly string storageAccountKey = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_KEY") ?? string.Empty;
    private static readonly string acrName = Environment.GetEnvironmentVariable("ACR_NAME") ?? string.Empty;
    private static readonly string acrPass = Environment.GetEnvironmentVariable("ACR_PASS") ?? string.Empty;
    private static readonly string iothubConnectorImage = Environment.GetEnvironmentVariable("IOTHUB_CONNECTOR_IMAGE") ?? string.Empty;
    private static readonly string weatherObserverImage = Environment.GetEnvironmentVariable("WEATHER_OBSERVER_IMAGE") ?? string.Empty;
    private static readonly string fileGeneratorImage = Environment.GetEnvironmentVariable("FILE_GENERATOR_IMAGE") ?? string.Empty;
    private static readonly string fileUploaderImage = Environment.GetEnvironmentVariable("FILE_UPLOADER_IMAGE") ?? string.Empty;
    private static readonly string fileUpdaterImage = Environment.GetEnvironmentVariable("FILE_UPDATER_IMAGE") ?? string.Empty;
    private static readonly string localBlobStorageImage = "mcr.microsoft.com/azure-blob-storage:latest";
    private static readonly string iotHubDeviceId = Environment.GetEnvironmentVariable("IOTHUB_DEVICE_ID") ?? string.Empty;
    private static readonly string iotHubConnectionString = Environment.GetEnvironmentVariable("IOTHUB_CONNECTION_STRING") ?? string.Empty;
    private static readonly string localStorageAccountKey = Environment.GetEnvironmentVariable("LOCAL_STORAGE_KEY") ?? string.Empty;
    private static readonly string orgName = Environment.GetEnvironmentVariable("ORGANIZATION_NAME") ?? string.Empty;

    public static async Task Main(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        string routeTelemetry = configuration.GetValue<string>("RouteTelemetry");
        string routeReportRequest = configuration.GetValue<string>("RouteReportRequest");
        string routeReportResponse = configuration.GetValue<string>("RouteReportResponse");
        string routeUpdateRequest = configuration.GetValue<string>("RouteUpdateRequest");
        string routeUpdateResponse = configuration.GetValue<string>("RouteUpdateResponse");
        string ros2topic = configuration.GetValue<string>("Ros2Topic");
        string fileGeneratorBind = configuration.GetValue<string>("FileGeneratorContainerBind");
        string fileUploaderBind = configuration.GetValue<string>("FileUploaderContainerBind");
        string fileUpdaterBind = configuration.GetValue<string>("FileUpdaterContainerBind");
        string fileBlobStorageBind = configuration.GetValue<string>("LocalBlobStorageBind");
        string cloudBlobContainerName = configuration.GetValue<string>("CloudBlobContainerName");
        string localBlobContainerName = configuration.GetValue<string>("LocalBlobContainerName");
        string localStorageAccountName = configuration.GetValue<string>("LocalBlobAccountName");
        string localStorageAccountEndpoint = configuration.GetValue<string>("LocalBlobEndpoint");
        string fileGeneratorWorkdir = configuration.GetValue<string>("FileGeneratorWorkdir");
        string fileUploaderWorkdir = configuration.GetValue<string>("FileUploaderWorkdir");
        string fileUpdaterWorkdir = configuration.GetValue<string>("FileUpdaterWorkdir");
        int sasExpirationMonths = configuration.GetValue<int>("SasExpirationMonths");

        string route_telemetry = $"FROM /messages/modules/IothubConnector/outputs/{routeTelemetry} INTO $upstream";
        string route_c2w = GenerateRoute("IothubConnector", routeReportRequest, "WeatherObserver", routeReportRequest);
        string route_w2c = GenerateRoute("WeatherObserver", routeReportResponse, "IothubConnector", routeReportResponse);
        string route_w2u = GenerateRoute("IothubConnector", routeUpdateRequest, "FileUpdater", routeUpdateRequest);
        string route_u2w = GenerateRoute("FileUpdater", routeUpdateResponse, "IothubConnector", routeUpdateResponse);

        string fileGeneratorCreateOptions = $"{{\"HostConfig\":{{\"Binds\":[\"{fileGeneratorBind}\"]}}}}";
        string fileUploaderCreateOptions = $"{{\"HostConfig\":{{\"Binds\":[\"{fileUploaderBind}\"]}}}}";
        string fileUpdaterCreateOptions = $"{{\"HostConfig\":{{\"Binds\":[\"{fileUpdaterBind}\"]}}}}";
        string localBlobStorageCreateOptions = $"{{\"HostConfig\":{{\"Binds\":[\"{fileBlobStorageBind}\"],\"PortBindings\":{{\"11002/tcp\":[{{\"HostPort\":\"11002\"}}]}}}}}}";

        StorageSharedKeyCredential sharedKeyCredential = new StorageSharedKeyCredential(storageAccountName, storageAccountKey);
        string dfsUri = "https://" + storageAccountName + ".dfs.core.windows.net";
        DataLakeServiceClient dataLakeServiceClient = new (new Uri(dfsUri), sharedKeyCredential);
        SasService sasService = new (dataLakeServiceClient);
        string cloudStorageSasConnectionString = await sasService.GenerateSasAsync(cloudBlobContainerName, orgName, sasExpirationMonths);

        List<EnvironmentVariable> iothubConnectorEnv = new ()
        {
            new EnvironmentVariable("ROS_TOPIC_NAME", ros2topic),
        };

        List<EnvironmentVariable> fileGeneratorEnv = new ()
        {
            new EnvironmentVariable("OUTPUT_DIRECTORY_PATH", fileGeneratorWorkdir),
            new EnvironmentVariable("ROS_TOPIC_NAME", ros2topic),
        };

        List<EnvironmentVariable> fileUploaderEnv = new ()
        {
            new EnvironmentVariable("LOCAL_STORAGE_ACCOUNT_NAME", localStorageAccountName),
            new EnvironmentVariable("LOCAL_STORAGE_ACCOUNT_KEY", localStorageAccountKey),
            new EnvironmentVariable("LOCAL_STORAGE_BLOB_ENDPOINT", localStorageAccountEndpoint),
            new EnvironmentVariable("WORKDIR", fileUploaderWorkdir),
        };
        List<EnvironmentVariable> fileUpdaterEnv = new ()
        {
            new EnvironmentVariable("WORKDIR", fileUpdaterWorkdir),
        };
        List<EnvironmentVariable> localBlobStorageEnv = new ()
        {
            new EnvironmentVariable("LOCAL_STORAGE_ACCOUNT_NAME", localStorageAccountName),
            new EnvironmentVariable("LOCAL_STORAGE_ACCOUNT_KEY", localStorageAccountKey),
        };


        EdgeAgentDesiredProperties edgeAgentDesiredProperties = new ()
        {
            SystemModuleVersion = "1.3",
            RegistryCredentials = new List<RegistryCredential>()
            {
                new RegistryCredential(acrName, $"{acrName}.azurecr.io", acrName, acrPass),
            },
            EdgeModuleSpecifications = new List<EdgeModuleSpecification>()
            {
                new EdgeModuleSpecification(name:"IothubConnector", image:iothubConnectorImage, environmentVariables:iothubConnectorEnv),
                new EdgeModuleSpecification(name:"WeatherObserver", image:weatherObserverImage),
                new EdgeModuleSpecification(name:"FileGenerator", image:fileGeneratorImage, createOptions:fileGeneratorCreateOptions, environmentVariables:fileGeneratorEnv),
                new EdgeModuleSpecification(name:"FileUploader", image:fileUploaderImage, createOptions:fileUploaderCreateOptions, environmentVariables:fileUploaderEnv),
                new EdgeModuleSpecification(name:"FileUpdater", image:fileUpdaterImage, createOptions:fileUpdaterCreateOptions, environmentVariables:fileUpdaterEnv),
                new EdgeModuleSpecification(name:"LocalBlobStorage", image:localBlobStorageImage, createOptions:localBlobStorageCreateOptions, environmentVariables:localBlobStorageEnv),
            },
        };
        EdgeHubDesiredProperties edgeHubConfig = new ()
        {
            Routes = new List<Route>()
            {
                new Route("route_telemetry", route_telemetry),
                new Route("route_c2w", route_c2w),
                new Route("route_w2c", route_w2c),
                new Route("route_w2u", route_w2u),
                new Route("route_u2w", route_u2w),
            },
        };
        ModuleSpecificationDesiredProperties iothubConnector = new ()
        {
            Name = "IothubConnector", DesiredProperties = new {},
        };
        ModuleSpecificationDesiredProperties weatherObserver = new ()
        {
            Name = "WeatherObserver", DesiredProperties = new {},
        };
        ModuleSpecificationDesiredProperties fileGenerator = new ()
        {
            Name = "FileGenerator", DesiredProperties = new {},
        };
        ModuleSpecificationDesiredProperties fileUploader = new ()
        {
            Name = "FileUploader", DesiredProperties = new {},
        };
        ModuleSpecificationDesiredProperties fileUpdater = new ()
        {
            Name = "FileUpdater", DesiredProperties = new {},
        };
        ModuleSpecificationDesiredProperties localBlobStorage = new ()
        {
            Name = "LocalBlobStorage",
            DesiredProperties = new Dictionary<string, object>
            {
                ["deviceAutoDeleteProperties"] = new Dictionary<string, object>
                {
                    ["deleteOn"] = true,
                    ["deleteAfterMinutes"] = 5,
                    ["retainWhileUploading"] = true,
                },
                ["deviceToCloudUploadProperties"] = new Dictionary<string, object>
                {
                    ["uploadOn"] = true,
                    ["uploadOrder"] = "NewestFirst",
                    ["deleteAfterUpload"] = true,
                    ["cloudStorageConnectionString"] = cloudStorageSasConnectionString,
                    ["storageContainersForUpload"] = new Dictionary<string, object>
                    {
                        [localBlobContainerName] = new Dictionary<string, object>
                        {
                            ["target"] = iotHubDeviceId,
                        }
                    },
                },
            },
        };
        ConfigurationContent configurationContent = new ConfigurationContent()
                        .SetEdgeHub(edgeHubConfig)
                        .SetEdgeAgent(edgeAgentDesiredProperties)
                        .SetModuleDesiredProperty(iothubConnector)
                        .SetModuleDesiredProperty(weatherObserver)
                        .SetModuleDesiredProperty(fileGenerator)
                        .SetModuleDesiredProperty(fileUploader)
                        .SetModuleDesiredProperty(fileUpdater)
                        .SetModuleDesiredProperty(localBlobStorage);

        string jsonString = System.Text.Json.JsonSerializer.Serialize(configurationContent, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        File.WriteAllText("manifest.json", jsonString);

        RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
        await registryManager.ApplyConfigurationContentOnDeviceAsync(iotHubDeviceId, configurationContent).ConfigureAwait(false);
    }

    public static string GenerateRoute(string requester, string requesterRoute, string receiver, string receiverRoute)
    {
        return $"FROM /messages/modules/{requester}/outputs/{requesterRoute} INTO BrokeredEndpoint(\"/modules/{receiver}/inputs/{receiverRoute}\")";
    }
}