﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

namespace Microsoft.WindowsAzure.Commands.ServiceManagement.HostedServices
{
    using System;
    using System.Management.Automation;
    using System.Net;
    using Extensions;
    using Helpers;
    using Management.Compute;
    using Management.Compute.Models;
    using Properties;
    using Utilities.Common;

    /// <summary>
    /// Update deployment configuration, upgrade or status
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "AzureDeployment"), OutputType(typeof(ManagementOperationContext))]
    public class SetAzureDeploymentCommand : ServiceManagementBaseCmdlet
    {
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Upgrade", HelpMessage = "Upgrade Deployment")]
        public SwitchParameter Upgrade
        {
            get;
            set;
        }

        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Config", HelpMessage = "Change Configuration of Deployment")]
        public SwitchParameter Config
        {
            get;
            set;
        }

        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Status", HelpMessage = "Change Status of Deployment")]
        public SwitchParameter Status
        {
            get;
            set;
        }

        [Parameter(Position = 1, Mandatory = true, ParameterSetName = "Upgrade", ValueFromPipelineByPropertyName = true, HelpMessage = "Service name")]
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = "Config", ValueFromPipelineByPropertyName = true, HelpMessage = "Service name")]
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = "Status", ValueFromPipelineByPropertyName = true, HelpMessage = "Service name")]
        [ValidateNotNullOrEmpty]
        public string ServiceName
        {
            get;
            set;
        }

        [Parameter(Position = 2, Mandatory = true, ParameterSetName = "Upgrade", HelpMessage = "Package location. This parameter should have the local file path or URI to a .cspkg in blob storage whose storage account is part of the same subscription/project.")]
        [ValidateNotNullOrEmpty]
        public string Package
        {
            get;
            set;
        }

        [Parameter(Position = 2, Mandatory = true, ParameterSetName = "Config", HelpMessage = "Configuration file path. This parameter should specifiy a .cscfg file on disk.")]
        [Parameter(Position = 3, Mandatory = true, ParameterSetName = "Upgrade", HelpMessage = "Configuration file path. This parameter should specifiy a .cscfg file on disk.")]
        [ValidateNotNullOrEmpty]
        public string Configuration
        {
            get;
            set;
        }

        [Parameter(Position = 4, Mandatory = true, ParameterSetName = "Upgrade", ValueFromPipelineByPropertyName = true, HelpMessage = "Deployment slot. Staging | Production")]
        [Parameter(Position = 3, Mandatory = true, ParameterSetName = "Config", ValueFromPipelineByPropertyName = true, HelpMessage = "Deployment slot. Staging | Production")]
        [Parameter(Position = 2, Mandatory = true, ParameterSetName = "Status", ValueFromPipelineByPropertyName = true, HelpMessage = "Deployment slot. Staging | Production")]
        [ValidateSet(Model.PersistentVMModel.DeploymentSlotType.Staging, Model.PersistentVMModel.DeploymentSlotType.Production, IgnoreCase = true)]
        public string Slot
        {
            get;
            set;
        }

        [Parameter(Position = 5, ParameterSetName = "Upgrade", HelpMessage = "Upgrade mode. Auto | Manual | Simultaneous")]
        [ValidateSet(Model.PersistentVMModel.UpgradeType.Auto, Model.PersistentVMModel.UpgradeType.Manual, Model.PersistentVMModel.UpgradeType.Simultaneous, IgnoreCase = true)]
        public string Mode
        {
            get;
            set;
        }

        [Parameter(Position = 6, Mandatory = false, ParameterSetName = "Upgrade", HelpMessage = "Label name for the new deployment. Default: <Service Name> + <date time>")]
        [ValidateNotNullOrEmpty]
        public string Label
        {
            get;
            set;
        }

        [Parameter(Position = 7, Mandatory = false, ParameterSetName = "Upgrade", HelpMessage = "Name of role to upgrade.")]
        public string RoleName
        {
            get;
            set;
        }

        [Parameter(Position = 8, Mandatory = false, ParameterSetName = "Upgrade", HelpMessage = "Force upgrade.")]
        public SwitchParameter Force
        {
            get;
            set;
        }

        [Parameter(Position = 3, Mandatory = true, ParameterSetName = "Status", HelpMessage = "New deployment status. Running | Suspended")]
        [ValidateSet(Model.PersistentVMModel.DeploymentStatus.Running, Model.PersistentVMModel.DeploymentStatus.Suspended, IgnoreCase = true)]
        public string NewStatus
        {
            get;
            set;
        }

        [Parameter(Position = 9, ValueFromPipelineByPropertyName = true, Mandatory = false, ParameterSetName = "Upgrade", HelpMessage = "Extension configurations.")]
        [Parameter(Position = 4, ValueFromPipelineByPropertyName = true, Mandatory = false, ParameterSetName = "Config", HelpMessage = "HelpMessage")]
        public ExtensionConfigurationInput[] ExtensionConfiguration
        {
            get;
            set;
        }

        public void ExecuteCommand()
        {
            string configString = string.Empty;
            if (!string.IsNullOrEmpty(Configuration))
            {
                configString = GeneralUtils.GetConfiguration(Configuration);
            }

            ExtensionConfiguration extConfig = null;
            if (ExtensionConfiguration != null)
            {
                string errorConfigInput = null;
                if (!ExtensionManager.Validate(ExtensionConfiguration, out errorConfigInput))
                {
                    throw new Exception(string.Format(Resources.ServiceExtensionCannotApplyExtensionsInSameType, errorConfigInput));
                }

                foreach (ExtensionConfigurationInput context in ExtensionConfiguration)
                {
                    if (context != null && context.X509Certificate != null)
                    {
                        ExecuteClientActionNewSM(
                            null,
                            string.Format(Resources.ServiceExtensionUploadingCertificate, CommandRuntime, context.X509Certificate.Thumbprint),
                            () => this.ComputeClient.ServiceCertificates.Create(this.ServiceName, CertUtilsNewSM.Create(context.X509Certificate)));
                    }
                }

                var slotType = (DeploymentSlot)Enum.Parse(typeof(DeploymentSlot), this.Slot, true);
                DeploymentGetResponse d = null;
                InvokeInOperationContext(() =>
                {
                    try
                    {
                        d = this.ComputeClient.Deployments.GetBySlot(this.ServiceName, slotType);
                    }
                    catch (CloudException ex)
                    {
                        if (ex.Response.StatusCode != HttpStatusCode.NotFound && IsVerbose() == false)
                        {
                            this.WriteExceptionDetails(ex);
                        }
                    }
                });

                ExtensionManager extensionMgr = new ExtensionManager(this, ServiceName);
                extConfig = extensionMgr.Add(d, ExtensionConfiguration, this.Slot);
            }

            // Upgrade Parameter Set
            if (string.Compare(ParameterSetName, "Upgrade", StringComparison.OrdinalIgnoreCase) == 0)
            {
                bool removePackage = false;
                var storageName = CurrentSubscription.CurrentStorageAccountName;

                Uri packageUrl = null;
                if (Package.StartsWith(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    Package.StartsWith(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    packageUrl = new Uri(Package);
                }
                else
                {
                    if (string.IsNullOrEmpty(storageName))
                    {
                        throw new ArgumentException(Resources.CurrentStorageAccountIsNotSet);
                    }

                    var progress = new ProgressRecord(0, Resources.WaitForUploadingPackage, Resources.UploadingPackage);
                    WriteProgress(progress);
                    removePackage = true;
                    InvokeInOperationContext(() => packageUrl = RetryCall(s => AzureBlob.UploadPackageToBlob(this.StorageClient, storageName, Package, null)));
                }

                DeploymentUpgradeMode upgradeMode;
                if (!Enum.TryParse<DeploymentUpgradeMode>(Mode, out upgradeMode))
                {
                    upgradeMode = DeploymentUpgradeMode.Auto;
                }

                var upgradeDeploymentInput = new DeploymentUpgradeParameters
                {
                    Mode = upgradeMode,
                    Configuration = configString,
                    ExtensionConfiguration = extConfig,
                    PackageUri = packageUrl,
                    Label = Label ?? ServiceName,
                    Force = Force.IsPresent
                };

                if (!string.IsNullOrEmpty(RoleName))
                {
                    upgradeDeploymentInput.RoleToUpgrade = RoleName;
                }

                InvokeInOperationContext(() =>
                {
                    try
                    {
                        ExecuteClientActionNewSM(
                            upgradeDeploymentInput,
                            CommandRuntime.ToString(),
                            () => this.ComputeClient.Deployments.UpgradeBySlot(
                                this.ServiceName,
                                (DeploymentSlot)Enum.Parse(typeof(DeploymentSlot), this.Slot, true),
                                upgradeDeploymentInput));

                        if (removePackage == true)
                        {
                            this.RetryCall(s =>
                            AzureBlob.DeletePackageFromBlob(
                                    this.StorageClient,
                                    storageName,
                                    packageUrl));
                        }
                    }
                    catch (CloudException ex)
                    {
                        this.WriteExceptionDetails(ex);
                    }
                });
            }
            else if (string.Compare(this.ParameterSetName, "Config", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Config parameter set
                var changeDeploymentStatusParams = new DeploymentChangeConfigurationParameters
                {
                    Configuration = configString,
                    ExtensionConfiguration = extConfig
                };

                ExecuteClientActionNewSM(
                    changeDeploymentStatusParams,
                    CommandRuntime.ToString(),
                    () => this.ComputeClient.Deployments.ChangeConfigurationBySlot(
                        this.ServiceName,
                        (DeploymentSlot)Enum.Parse(typeof(DeploymentSlot), this.Slot, true),
                        changeDeploymentStatusParams));
            }
            else
            {
                // Status parameter set
                var updateDeploymentStatusParams = new DeploymentUpdateStatusParameters
                {
                    Status = (UpdatedDeploymentStatus)Enum.Parse(typeof(UpdatedDeploymentStatus), this.NewStatus, true)
                };

                ExecuteClientActionNewSM(
                    null,
                    CommandRuntime.ToString(),
                    () => this.ComputeClient.Deployments.UpdateStatusByDeploymentSlot(
                    this.ServiceName,
                    (DeploymentSlot)Enum.Parse(typeof(DeploymentSlot), this.Slot, true),
                    updateDeploymentStatusParams));
            }
        }

        protected override void OnProcessRecord()
        {
            ServiceManagementProfile.Initialize();
            this.ExecuteCommand();
        }
    }
}
