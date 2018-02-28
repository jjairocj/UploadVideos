using System;
using System.Configuration;
using System.IO;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace UploadVideos
{
    class Program
    {
        private static readonly string _AADTenantDomain = ConfigurationManager.AppSettings["AMSAADTenantDomain"];

        private static readonly string _RESTAPIEndpoint = ConfigurationManager.AppSettings["AMSRESTAPIEndpoint"];

        private static readonly string _AMSClientId = ConfigurationManager.AppSettings["AMSClientId"];

        private static readonly string _AMSClientSecret = ConfigurationManager.AppSettings["AMSClientSecret"];

        private static CloudMediaContext _context = null;

        static void Main(string[] args)
        {
          
            // Specify your Azure AD tenant domain, for example "microsoft.onmicrosoft.com".
            var tokenCredentials = new AzureAdTokenCredentials("jcruzterecomiendo.onmicrosoft.com", AzureEnvironments.AzureCloudEnvironment);

            var tokenProvider = new AzureAdTokenProvider(tokenCredentials);

            // Specify your REST API endpoint, for example "https://terecomiendoms.restv2.eastus.media.azure.net/api/".
            CloudMediaContext context = new CloudMediaContext(new Uri("https://terecomiendoms.restv2.eastus.media.azure.net/api/"), tokenProvider);

            var fileUrl = Console.ReadLine();
            if(fileUrl != "0")
            {

            
            var file = CreateAssetAndUploadSingleFile(AssetCreationOptions.StorageEncrypted, fileUrl);

            var asset = EncodeToAdaptiveBitrateMP4Set(file);

            }

            var assets = context.Assets;
            
            foreach (var a in assets)
            {
                Console.WriteLine(a.Name);
                BuildStreamingURLs(a);
            }

            Console.ReadKey();
        }

        static public IAsset CreateAssetAndUploadSingleFile(AssetCreationOptions assetCreationOptions, string singleFilePath)
        {
            if (!File.Exists(singleFilePath))
            {
                Console.WriteLine("File does not exist.");
                return null;
            }

            var assetName = Path.GetFileNameWithoutExtension(singleFilePath);
            IAsset inputAsset = _context.Assets.Create(assetName, assetCreationOptions);

            var assetFile = inputAsset.AssetFiles.Create(Path.GetFileName(singleFilePath));

            Console.WriteLine("Upload {0}", assetFile.Name);

            assetFile.Upload(singleFilePath);
            Console.WriteLine("Done uploading {0}", assetFile.Name);

            return inputAsset;
        }
        static public IAsset EncodeToAdaptiveBitrateMP4Set(IAsset asset)
        {
            // Declare a new job.
            IJob job = _context.Jobs.Create("Media Encoder Standard Job");
            // Get a media processor reference, and pass to it the name of the 
            // processor to use for the specific task.
            IMediaProcessor processor = GetLatestMediaProcessorByName("Media Encoder Standard");

            // Create a task with the encoding details, using a string preset.
            // In this case "Adaptive Streaming" preset is used.
            ITask task = job.Tasks.AddNew("My encoding task",
                processor,
                "Adaptive Streaming",
                TaskOptions.None);

            // Specify the input asset to be encoded.
            task.InputAssets.Add(asset);
            // Add an output asset to contain the results of the job. 
            // This output is specified as AssetCreationOptions.None, which 
            // means the output asset is not encrypted. 
            task.OutputAssets.AddNew("Output asset",
                AssetCreationOptions.None);

            job.StateChanged += new EventHandler<JobStateChangedEventArgs>(JobStateChanged);
            job.Submit();
            job.GetExecutionProgressTask(CancellationToken.None).Wait();

            return job.OutputMediaAssets[0];
        }

        private static void JobStateChanged(object sender, JobStateChangedEventArgs e)
        {
            Console.WriteLine("Job state changed event:");
            Console.WriteLine("  Previous state: " + e.PreviousState);
            Console.WriteLine("  Current state: " + e.CurrentState);
            switch (e.CurrentState)
            {
                case JobState.Finished:
                    Console.WriteLine();
                    Console.WriteLine("Job is finished. Please wait while local tasks or downloads complete...");
                    break;
                case JobState.Canceling:
                case JobState.Queued:
                case JobState.Scheduled:
                case JobState.Processing:
                    Console.WriteLine("Please wait...\n");
                    break;
                case JobState.Canceled:
                case JobState.Error:

                    // Cast sender as a job.
                    IJob job = (IJob)sender;

                    // Display or log error details as needed.
                    break;
                default:
                    break;
            }
        }

        private static IMediaProcessor GetLatestMediaProcessorByName(string mediaProcessorName)
        {
            var processor = _context.MediaProcessors.Where(p => p.Name == mediaProcessorName).
            ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();

            if (processor == null)
                throw new ArgumentException(string.Format("Unknown media processor", mediaProcessorName));

            return processor;
        }

        private static void BuildStreamingURLs(IAsset asset)
        {

            // Create a 30-day readonly access policy. 
            // You cannot create a streaming locator using an AccessPolicy that includes write or delete permissions.
            IAccessPolicy policy = _context.AccessPolicies.Create("Streaming policy",
                TimeSpan.FromDays(30),
                AccessPermissions.Read);

            // Create a locator to the streaming content on an origin. 
            ILocator originLocator = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, asset,
                policy,
                DateTime.UtcNow.AddMinutes(-5));

            // Display some useful values based on the locator.
            Console.WriteLine("Streaming asset base path on origin: ");
            Console.WriteLine(originLocator.Path);
            Console.WriteLine();

            // Get a reference to the streaming manifest file from the  
            // collection of files in the asset. 
            var manifestFile = asset.AssetFiles.Where(f => f.Name.ToLower().
                                        EndsWith(".ism")).
                                        FirstOrDefault();

            // Create a full URL to the manifest file. Use this for playback
            // in streaming media clients. 
            string urlForClientStreaming = originLocator.Path + manifestFile.Name + "/manifest";
            Console.WriteLine("URL to manifest for client streaming using Smooth Streaming protocol: ");
            Console.WriteLine(urlForClientStreaming);
            Console.WriteLine("URL to manifest for client streaming using HLS protocol: ");
            Console.WriteLine(urlForClientStreaming + "(format=m3u8-aapl)");
            Console.WriteLine("URL to manifest for client streaming using MPEG DASH protocol: ");
            Console.WriteLine(urlForClientStreaming + "(format=mpd-time-csf)");
            Console.WriteLine();
        }
    }
}
