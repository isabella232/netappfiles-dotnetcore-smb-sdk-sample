// Copyright (c) Microsoft and contributors.  All rights reserved.
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.

namespace Microsoft.Azure.Management.ANF.Samples.Common
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Management.NetApp;
    using Microsoft.Azure.Management.NetApp.Models;

    /// <summary>
    /// Contains public methods for SDK related operations
    /// </summary>
    public static class Sdk
    {
        /// <summary>
        /// Returns an ANF resource or null if it does not exist
        /// </summary>
        /// <typeparam name="T">Valid types: NetAppAccount, CapacityPool, Volume, Snapshot</typeparam>
        /// <param name="client">ANF Client object</param>
        /// <param name="parameterList">List of parameters required depending on the resource type:
        ///     Snapshot     -> ResourceGroupName, AccountName, PoolName, VolumeName, SnapshotName
        ///     Volume       -> ResourceGroupName, AccountName, PoolName, VolumeName
        ///     CapacityPool -> ResourceGroupName, AccountName, PoolName
        ///     Account      -> ResourceGroupName, AccountName</param>
        /// <returns></returns>
        public static async Task<T> GetResourceAsync<T>(AzureNetAppFilesManagementClient client, params string[] parameterList)
        {
            try
            {
                if (typeof(T) == typeof(Snapshot))
                {
                    return (T)(object)await client.Snapshots.GetAsync(
                        resourceGroupName: parameterList[0],
                        accountName: parameterList[1],
                        poolName: parameterList[2],
                        volumeName: parameterList[3],
                        snapshotName: parameterList[4]);
                }
                else if (typeof(T) == typeof(Volume))
                {
                    return (T)(object)await client.Volumes.GetAsync(
                        resourceGroupName: parameterList[0],
                        accountName: parameterList[1],
                        poolName: parameterList[2],
                        volumeName: parameterList[3]);
                }
                else if (typeof(T) == typeof(CapacityPool))
                {
                    return (T)(object)await client.Pools.GetAsync(
                        resourceGroupName: parameterList[0],
                        accountName: parameterList[1],
                        poolName: parameterList[2]);
                }
                else if (typeof(T) == typeof(NetAppAccount))
                {
                    return (T)(object)await client.Accounts.GetAsync(
                        resourceGroupName: parameterList[0],
                        accountName: parameterList[1]);
                }
            }
            catch (Exception ex)
            {
                // The following HResult is thrown if no resource is found
                if (ex.HResult != -2146233088)
                {
                    throw;
                }
            }

            // If object is not supported by this method or nothing returned from GetAsync, return null
            return default(T);
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
        static public async Task WaitForNoAnfResource<T>(AzureNetAppFilesManagementClient client, string resourceId, int intervalInSec = 10, int retries = 60)
        {
            for (int i = 0; i < retries; i++)
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

        /// <summary>
        /// Function used to wait for a specific ANF resource complete its deletion and ARM caching gets cleared
        /// </summary>
        /// <typeparam name="T">Resource Types as Snapshot, Volume, CapacityPool, and NetAppAccount</typeparam>
        /// <param name="client">ANF Client</param>
        /// <param name="resourceId">Resource Id of the resource being waited for being deleted</param>
        /// <param name="intervalInSec">Time in seconds that the sample will poll to check if the resource got deleted or not. Defaults to 10 seconds.</param>
        /// <param name="retries">How many retries before exting the wait for no resource function. Defaults to 60 retries.</param>
        /// <returns></returns>
        static public async Task WaitForAnfResource<T>(AzureNetAppFilesManagementClient client, string resourceId, int intervalInSec = 10, int retries = 60)
        {
            bool isFound = false;

            for (int i = 0; i < retries; i++)
            {
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(intervalInSec));

                try
                {
                    if (typeof(T) == typeof(NetAppAccount))
                    {
                        await client.Accounts.GetAsync(ResourceUriUtils.GetResourceGroup(resourceId),
                            ResourceUriUtils.GetAnfAccount(resourceId));
                    }
                    else if (typeof(T) == typeof(CapacityPool))
                    {
                        await client.Pools.GetAsync(ResourceUriUtils.GetResourceGroup(resourceId),
                            ResourceUriUtils.GetAnfAccount(resourceId),
                            ResourceUriUtils.GetAnfCapacityPool(resourceId));

                    }
                    else if (typeof(T) == typeof(Volume))
                    {
                        await client.Volumes.GetAsync(ResourceUriUtils.GetResourceGroup(resourceId),
                            ResourceUriUtils.GetAnfAccount(resourceId),
                            ResourceUriUtils.GetAnfCapacityPool(resourceId),
                            ResourceUriUtils.GetAnfVolume(resourceId));

                    }
                    else if (typeof(T) == typeof(Snapshot))
                    {
                        await client.Snapshots.GetAsync(ResourceUriUtils.GetResourceGroup(resourceId),
                            ResourceUriUtils.GetAnfAccount(resourceId),
                            ResourceUriUtils.GetAnfCapacityPool(resourceId),
                            ResourceUriUtils.GetAnfVolume(resourceId),
                            ResourceUriUtils.GetAnfSnapshot(resourceId));
                    }
                    isFound = true;
                    break;
                }
                catch
                {
                    continue;
                }
            }
            if (!isFound)
                throw new Exception($"Resource: {resourceId} is not found");

        }
    }
}
