// Copyright (c) Microsoft and contributors.  All rights reserved.
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.

namespace Microsoft.Azure.Management.ANF.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Azure.Management.ANF.Samples.Common;
    using Microsoft.Azure.Management.NetApp;
    using Microsoft.Azure.Management.NetApp.Models;
    using static Microsoft.Azure.Management.ANF.Samples.Common.Sdk;
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
            bool cleanup = false;
            string subscriptionId = "[Subscription Id]";
            string location = "[Location]";
            string resourceGroupName = "[Resource group name where ANF resources will be created]";
            string vnetName = "[Existing Vnet Name]";
            string subnetName = "[Existing Subnet where ANF volumes will be created]";
            string vnetResourceGroupName = "[Vnet Resource Group Name]";
            string anfAccountName = "[ANF Account Name]";
            string capacityPoolName = "[ANF Capacity Pool Name]";
            string capacityPoolServiceLevel = "Standard"; // Valid service levels are: Standard, Premium and Ultra
            long capacitypoolSize = 4398046511104;  // 4TiB which is minimum size
            long volumeSize = 107374182400;  // 100GiB - volume minimum size

            // SMB/CIFS related variables
            string domainJoinUsername = "[Domain user with permissions to create computer accounts]";
            string dnsList = "[DNS Ip Address]"; // Please notice that this is a comma-separated string
            string adFQDN = "[Active Directory FQDN]";
            string smbServerNamePrefix = "[SMB Server Name Prefix]"; // this needs to be maximum 10 characters in length and during the domain join process a random string gets appended.

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
            NetAppAccount anfAccount = await GetResourceAsync<NetAppAccount>(anfClient, resourceGroupName, anfAccountName);
            if (anfAccount == null)
            {
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
                    Location = location.ToLower(), // Important: location needs to be lower case,
                    ActiveDirectories = activeDirectories
                };

                // Requesting account to be created
                WriteConsoleMessage("Creating account...");
                anfAccount = await anfClient.Accounts.CreateOrUpdateAsync(anfAccountBody, resourceGroupName, anfAccountName);
            }
            else
            {
                WriteConsoleMessage("Account already exists...");
            }
            WriteConsoleMessage($"\tAccount Resource Id: {anfAccount.Id}");

            //-----------------------
            // Creating Capacity Pool
            //-----------------------
            CapacityPool capacityPool = await GetResourceAsync<CapacityPool>(anfClient, resourceGroupName, anfAccountName, capacityPoolName);
            if (capacityPool == null)
            {
                // Setting up capacity pool body  object
                CapacityPool capacityPoolBody = new CapacityPool()
                {
                    Location = location.ToLower(),
                    ServiceLevel = capacityPoolServiceLevel,
                    Size = capacitypoolSize
                };

                // Creating capacity pool
                WriteConsoleMessage("Creating capacity pool...");
                capacityPool = await anfClient.Pools.CreateOrUpdateAsync(capacityPoolBody, resourceGroupName, anfAccount.Name, capacityPoolName);
            }
            else
            {
                WriteConsoleMessage("Capacity pool already exists...");
            }
            WriteConsoleMessage($"\tCapacity Pool Resource Id: {capacityPool.Id}");

            //------------------------
            // Creating SMB Volume
            //------------------------
            string volumeName = $"Vol-{anfAccountName}-{capacityPoolName}";

            Volume volume = await GetResourceAsync<Volume>(anfClient, resourceGroupName, anfAccountName, ResourceUriUtils.GetAnfCapacityPool(capacityPool.Id), volumeName);
            if (volume == null)
            {
                string subnetId = $"/subscriptions/{subscriptionId}/resourceGroups/{vnetResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{vnetName}/subnets/{subnetName}";

                // Creating volume body object
                Volume volumeBody = new Volume()
                {
                    Location = location.ToLower(),
                    ServiceLevel = capacityPoolServiceLevel,
                    CreationToken = volumeName,
                    SubnetId = subnetId,
                    UsageThreshold = volumeSize,
                    ProtocolTypes = new List<string>() { "CIFS" } // Despite of this being a list, only one protocol is supported at this time
                };

                // Creating SMB volume
                // Please notice that the SMB Server gets created at this point by using information stored in ANF Account resource about Active Directory
                WriteConsoleMessage("Creating volume...");
                volume = await anfClient.Volumes.CreateOrUpdateAsync(volumeBody, resourceGroupName, anfAccount.Name, ResourceUriUtils.GetAnfCapacityPool(capacityPool.Id), volumeName);
            }
            else
            {
                WriteConsoleMessage("Volume already exists...");
            }
            WriteConsoleMessage($"\tVolume Resource Id: {volume.Id}");

            //// Outputs SMB Server Name
            WriteConsoleMessage($"\t====> SMB Server FQDN: {volume.MountTargets[0].SmbServerFqdn}");

            //------------------------
            // Cleaning up
            //------------------------
            if (cleanup)
            {
                WriteConsoleMessage("Cleaning up created resources...");

                WriteConsoleMessage("\tDeleting volume...");
                await anfClient.Volumes.DeleteAsync(resourceGroupName, anfAccount.Name, ResourceUriUtils.GetAnfCapacityPool(capacityPool.Id), ResourceUriUtils.GetAnfVolume(volume.Id));
                // Adding a final verification if the resource completed deletion since it may have a few secs between ARM the Resource Provider be fully in sync
                await WaitForNoAnfResource<Volume>(anfClient, volume.Id);
                Utils.WriteConsoleMessage($"\t\tDeleted volume: {volume.Id}");

                WriteConsoleMessage("\tDeleting capacity pool...");
                await anfClient.Pools.DeleteAsync(resourceGroupName, anfAccount.Name, ResourceUriUtils.GetAnfCapacityPool(capacityPool.Id));
                await WaitForNoAnfResource<CapacityPool>(anfClient, capacityPool.Id);
                Utils.WriteConsoleMessage($"\t\tDeleted capacity pool: {capacityPool.Id}");

                WriteConsoleMessage("\tDeleting account...");
                await anfClient.Accounts.DeleteAsync(resourceGroupName, anfAccount.Name);
                await WaitForNoAnfResource<NetAppAccount>(anfClient, anfAccount.Id);
                Utils.WriteConsoleMessage($"\t\tDeleted account: {anfAccount.Id}");
            }
        }
    }
}
