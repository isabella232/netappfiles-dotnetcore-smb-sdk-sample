// Copyright (c) Microsoft and contributors.  All rights reserved.
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.

namespace Microsoft.Azure.Management.ANF.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Management.ANF.Samples.Common;
    using Microsoft.Azure.Management.NetApp;
    using Microsoft.Azure.Management.NetApp.Models;
    using static Microsoft.Azure.Management.ANF.Samples.Common.Utils;

    class program
    {
        /// <summary>
        /// Sample console application that execute an ANF Account, Capacity Pool and a Volume enabled with SMB/CIFS protocol
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            DisplayConsoleAppHeader();

            try
            {
                RunAsync().GetAwaiter().GetResult();
                Utils.WriteConsoleMessage("Sample application successfuly completed execution.");
            }
            catch (Exception ex)
            {
                WriteErrorMessage(ex.Message);
            }
        }

        static private async Task RunAsync()
        {
            //---------------------------------------------------------------------------------------------------------------------
            // Setting variables necessary for resources creation - change these to appropriated values related to your environment
            //---------------------------------------------------------------------------------------------------------------------
            string subscriptionId = "<subscription Id>";
            string location = "eastus2";
            string resourceGroupName = "anf01-rg";
            string vnetName = "vnet-02";
            string subnetName = "anf-sn";
            string vnetResourceGroupName = "anf01-rg";
            string anfAccountName = "anfaccount99";
            string capacityPoolName = "Pool01";
            string capacityPoolServiceLevel = "Standard";
            long capacitypoolSize = 4398046511104;  // 4TiB which is minimum size
            long volumeSize = 107374182400;  // 100GiB - volume minimum size

            // SMB/CIFS related variables
            string domainJoinUsername = "pmcadmin";
            string dnsList = "10.0.2.4,10.0.2.5"; // Please notice that this is a comma-separated string
            string adFQDN = "testdomain.local";
            string smbServerNamePrefix = "pmcsmb"; // this needs to be maximum 10 characters in length and during the domain join process a random string gets appended.

            //------------------------------------------------------------------------------------------------------
            // Getting Active Directory Identity's password (from identity that has rights to domain join computers) 
            //------------------------------------------------------------------------------------------------------
            Console.WriteLine("Please type Active Directory's user password that will domain join ANF's SMB server and press [ENTER]:");

            string DomainJoinUserPassword = Utils.GetConsolePassword();

            // Basic validation
            if (string.IsNullOrWhiteSpace(DomainJoinUserPassword))
            {
                throw new Exception("Invalid password, password cannot be null or empty string");
            }

            //----------------------------------------------------------------------------------------
            // Authenticating using service principal, refer to README.md file for requirement details
            //----------------------------------------------------------------------------------------
            WriteConsoleMessage("Authenticating...");
            var credentials = await ServicePrincipalAuth.GetServicePrincipalCredential("AZURE_AUTH_LOCATION");

            //------------------------------------------
            // Instantiating a new ANF management client
            //------------------------------------------
            WriteConsoleMessage("Instantiating a new Azure NetApp Files management client...");
            AzureNetAppFilesManagementClient anfClient = new AzureNetAppFilesManagementClient(credentials) { SubscriptionId = subscriptionId };
            WriteConsoleMessage($"\tApi Version: {anfClient.ApiVersion}");

            //----------------------
            // Creating ANF Account
            //----------------------

            // Setting up Active Directories Object
            // Despite of this being a list, currently ANF accepts only one Active Directory object and only one Active Directory should exist per subscription.
            List<ActiveDirectory> activeDirectories = new List<ActiveDirectory>()
            {
                new ActiveDirectory()
                {
                    Dns = dnsList,
                    Domain = adFQDN,
                    Username = domainJoinUsername,
                    Password = DomainJoinUserPassword,
                    SmbServerName = smbServerNamePrefix
                }
            };

            // Setting up NetApp Files account body  object
            NetAppAccount anfAccountBody = new NetAppAccount()
            {
                Location = location,
                ActiveDirectories = activeDirectories
            };

            // Requesting account to be created
            WriteConsoleMessage("Requesting account to be created...");
            var anfAccount = await anfClient.Accounts.CreateOrUpdateAsync(anfAccountBody, resourceGroupName, anfAccountName);
            WriteConsoleMessage($"\tAccount Resource Id: {anfAccount.Id}");

            //-----------------------
            // Creating Capacity Pool
            //-----------------------

            // Setting up capacity pool body  object
            CapacityPool capacityPoolBody = new CapacityPool()
            {
                Location = location.ToLower(), // Important: location needs to be lower case
                ServiceLevel = capacityPoolServiceLevel,
                Size = capacitypoolSize
            };

            // Creating capacity pool
            WriteConsoleMessage("Requesting capacity pool to be created...");
            var capacityPool = await anfClient.Pools.CreateOrUpdateAsync(capacityPoolBody, resourceGroupName, anfAccount.Name, capacityPoolName);
            WriteConsoleMessage($"\tCapacity Pool Resource Id: {capacityPool.Id}");

            //------------------------
            // Creating SMB Volume
            //------------------------

            // Creating volume body object
            string subnetId = $"/subscriptions/{subscriptionId}/resourceGroups/{vnetResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{vnetName}/subnets/{subnetName}";
            string volumeName = $"Vol-{anfAccountName}-{capacityPoolName}";

            Volume volumeBody = new Volume()
            {
                Location = location.ToLower(),
                ServiceLevel = capacityPoolServiceLevel,
                CreationToken = volumeName,
                SubnetId = subnetId,
                UsageThreshold = volumeSize,
                ProtocolTypes = new List<string>() { "CIFS" }
            };

            // Creating SMB volume
            // Please notice that the SMB Server gets created at this point by using information stored in ANF Account resource about Active Directory
            WriteConsoleMessage("Requesting volume to be created...");
            var volume = await anfClient.Volumes.CreateOrUpdateAsync(volumeBody, resourceGroupName, anfAccount.Name, ResourceUriUtils.GetAnfCapacityPool(capacityPool.Id), volumeName);
            WriteConsoleMessage($"\tVolume Resource Id: {volume.Id}");

            // Outputs SMB Server Name
            WriteConsoleMessage($"====> SMB Server FQDN: {volume.MountTargets}");


            //------------------------
            // Cleaning up
            //------------------------
            //WriteConsoleMessage("Cleaning up created resources...");

            //WriteConsoleMessage("\tDeleting volume...");
            //await anfClient.Volumes.DeleteAsync(resourceGroupName, anfAccount.Name, ResourceUriUtils.GetAnfCapacityPool(capacityPool.Id), ResourceUriUtils.GetAnfVolume(volume.Id));
            //// Adding a final verification if the resource completed deletion since it may have a few secs between ARM the Resource Provider be fully in sync
            //await WaitForNoAnfResource<Volume>(anfClient, volume.Id);
            //Utils.WriteConsoleMessage($"\t\tDeleted volume: {volume.Id}");

            //WriteConsoleMessage("\tDeleting capacity pool...");
            //await anfClient.Pools.DeleteAsync(resourceGroupName, anfAccount.Name, ResourceUriUtils.GetAnfCapacityPool(capacityPool.Id));
            //await WaitForNoAnfResource<CapacityPool>(anfClient, capacityPool.Id);
            //Utils.WriteConsoleMessage($"\t\tDeleted capacity pool: {capacityPool.Id}");

            //WriteConsoleMessage("\tDeleting account...");
            //await anfClient.Accounts.DeleteAsync(resourceGroupName, anfAccount.Name);
            //await WaitForNoAnfResource<NetAppAccount>(anfClient, anfAccount.Id);
            //Utils.WriteConsoleMessage($"\t\tDeleted account: {anfAccount.Id}");
        }

        /// <summary>
        /// Function used to wait for a specific ANF resource complete its deletion and ARM caching gets cleared
        /// </summary>
        /// <typeparam name="T">Resource Types as Snapshot, Volume, CapacityPool, and NetAppAccount</typeparam>
        /// <param name="client">ANF Client</param>
        /// <param name="resourceId">Resource Id of the resource being waited for being deleted</param>
        /// <param name="intervalInSec">Time in seconds that the sample will poll to check if the resource got deleted or not. Defaults to 10 seconds.</param>
        /// <param name="retries">How many retries before exting the wait for no resource function. Defaults to 60 retries.</param>
        /// <returns></returns>
        static private async Task WaitForNoAnfResource<T>(AzureNetAppFilesManagementClient client, string resourceId, int intervalInSec = 10, int retries = 60)
        {
            for (int i=0; i < retries; i++)
            {
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(intervalInSec));

                try
                {
                    if (typeof(T) == typeof(Snapshot))
                    {
                        var resource = await client.Snapshots.GetAsync(ResourceUriUtils.GetResourceGroup(resourceId),
                            ResourceUriUtils.GetAnfAccount(resourceId),
                            ResourceUriUtils.GetAnfCapacityPool(resourceId),
                            ResourceUriUtils.GetAnfVolume(resourceId),
                            ResourceUriUtils.GetAnfSnapshot(resourceId));
                    }
                    else if (typeof(T) == typeof(Volume))
                    {
                        var resource = await client.Volumes.GetAsync(ResourceUriUtils.GetResourceGroup(resourceId),
                            ResourceUriUtils.GetAnfAccount(resourceId),
                            ResourceUriUtils.GetAnfCapacityPool(resourceId),
                            ResourceUriUtils.GetAnfVolume(resourceId));
                    }
                    else if (typeof(T) == typeof(CapacityPool))
                    {
                        var resource = await client.Pools.GetAsync(ResourceUriUtils.GetResourceGroup(resourceId),
                            ResourceUriUtils.GetAnfAccount(resourceId),
                            ResourceUriUtils.GetAnfCapacityPool(resourceId));
                    }
                    else if (typeof(T) == typeof(NetAppAccount))
                    {
                        var resource = await client.Accounts.GetAsync(ResourceUriUtils.GetResourceGroup(resourceId),
                            ResourceUriUtils.GetAnfAccount(resourceId));
                    }
                }
                catch (Exception ex)
                {
                    // The following HResult is thrown if no resource is found
                    if (ex.HResult == -2146233088)
                    {
                        break;
                    }
                    throw;
                }
            }
        }
    }
}
