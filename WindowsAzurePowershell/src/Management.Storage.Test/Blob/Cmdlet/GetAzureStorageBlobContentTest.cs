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
// ---------------------------------------------------------------------------------

namespace Microsoft.WindowsAzure.Management.Storage.Test.Blob.Cmdlet
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Management.Storage.Blob;
    using Microsoft.WindowsAzure.Management.Storage.Blob.Cmdlet;
    using Microsoft.WindowsAzure.Management.Storage.Common;
    using Microsoft.WindowsAzure.Management.Test.Tests.Utilities;
    using Microsoft.WindowsAzure.ServiceManagement.Storage.Blob.Contract;
    using Microsoft.WindowsAzure.ServiceManagement.Storage.Blob.ResourceModel;
    using Microsoft.WindowsAzure.Storage.Blob;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Text;

    [TestClass]
    public class GetAzureStorageBlobContentTest : StorageBlobTestBase
    {
        internal FakeGetAzureStorageBlobContentCommand command = null;

        [TestInitialize]
        public void InitCommand()
        {
            command = new FakeGetAzureStorageBlobContentCommand(BlobMock)
                {
                    CommandRuntime = new MockCommandRuntime()
                };
        }

        [TestCleanup]
        public void CleanCommand()
        {
            command = null;
        }

        [TestMethod]
        public void OnStartTest()
        {
            ProgressRecord pr = null;
            command.OnStart(pr);
            pr = new ProgressRecord(0, "a", "b");
            pr.PercentComplete = 10;
            command.OnStart(pr);
            Assert.AreEqual(0, pr.PercentComplete);
        }

        [TestMethod]
        public void OnProgressTest()
        {
            ProgressRecord pr = null;
            command.OnProgress(pr, 0.0, 0.0);
            pr = new ProgressRecord(0, "a", "b");
            pr.PercentComplete = 10;
            command.OnProgress(pr, 5.6, 12.3);
            Assert.AreEqual(12, pr.PercentComplete);
            command.OnProgress(pr, 5.6, 12.8);
            Assert.AreEqual(12, pr.PercentComplete);
            command.OnProgress(pr, 5.6, 112.8);
            Assert.AreEqual(100, pr.PercentComplete);
            command.OnProgress(pr, 5.6, -112.8);
            Assert.AreEqual(0, pr.PercentComplete);
            command.OnProgress(pr, 5.6, 5);
            Assert.AreEqual(5, pr.PercentComplete);
        }

        [TestMethod]
        public void OnFinishTest()
        {
            ProgressRecord pr = null;
            ArgumentException e = new ArgumentException("test");
            command.OnFinish(pr, null);
            pr = new ProgressRecord(0, "a", "b");
            command.OnFinish(pr, null);
            Assert.AreEqual(100, pr.PercentComplete);
            Assert.AreEqual(String.Format(Resources.DownloadBlobSuccessful, ""), pr.StatusDescription);
            command.OnFinish(pr, e);
            Assert.AreEqual(100, pr.PercentComplete);
            Assert.AreEqual(String.Format(Resources.DownloadBlobFailed, "", "", "", e.Message), pr.StatusDescription);
        }

        [TestMethod]
        public void DownloadBlobTest()
        {
            command.DownloadBlob(null, null);
        }

        [TestMethod]
        public void GetBlobContentByNameWithInvalidNameTest()
        {
            string containerName = string.Empty;
            string blobName = string.Empty;
            string fileName = string.Empty;
            AssertThrows<ArgumentException>(() => command.GetBlobContent(containerName, blobName, fileName),
                String.Format(Resources.InvalidBlobName, blobName));

            containerName = "container1";
            blobName = "blob0";
            fileName = "blob*";
            AssertThrows<ArgumentException>(() => command.GetBlobContent(containerName, blobName, fileName),
                String.Format(Resources.InvalidFileName, fileName));
        }

        [TestMethod]
        public void GetBlobContentByNameSuccessfullyTest()
        {
            AddTestBlobs();

            string containerName = "container1";
            string blobName = "blob0";
            string fileName = string.Empty;

            AzureStorageBlob blob = command.GetBlobContent(containerName, blobName, fileName);
            Assert.AreEqual("blob0", blob.Name);
        }

        [TestMethod]
        public void GetBlobContentByContainerByInvalidNameTest()
        {
            CloudBlobContainer container = null;
            string blobName = string.Empty;
            string fileName = string.Empty;
            
            AssertThrows<ArgumentException>(() => command.GetBlobContent(container, blobName, fileName),
                String.Format(Resources.InvalidBlobName, blobName));

            blobName = "blob0";
            fileName = "ab*+";
            AssertThrows<ArgumentException>(() => command.GetBlobContent(container, blobName, fileName),
                String.Format(Resources.InvalidFileName, fileName));

            fileName = string.Empty;
            AssertThrows<ArgumentException>(() => command.GetBlobContent(container, blobName, fileName),
                String.Format(Resources.ObjectCannotBeNull, typeof(CloudBlobContainer).Name));
        }

        [TestMethod]
        public void GetBlobContentByContainerWithNotExistBlobTest()
        {
            AddTestContainers();
            CloudBlobContainer container = BlobMock.GetContainerReference("test");
            string blobName = "blob0";
            string fileName = string.Empty;
            AssertThrows<ResourceNotFoundException>(() => command.GetBlobContent(container, blobName, fileName),
                String.Format(Resources.BlobNotFound, blobName, container.Name));
        }

        [TestMethod]
        public void GetBlobContentByContainerSuccessfullyTest()
        {
            AddTestBlobs();

            CloudBlobContainer container = BlobMock.GetContainerReference("container20");
            string blobName = "blob10";
            string fileName = string.Empty;
            AzureStorageBlob blob = command.GetBlobContent(container, blobName, fileName);
            Assert.AreEqual("blob10", blob.Name);

            container = BlobMock.GetContainerReference("container20");
            blobName = "blob10";
            fileName = GetUniqueString();
            blob = command.GetBlobContent(container, blobName, fileName);
            Assert.AreEqual("blob10", blob.Name);
        }

        [TestMethod]
        public void GetBlobContentByICloudBlobWithInvalidArgumentsTest()
        {
            CloudBlockBlob blockBlob = null;
            string fileName = String.Empty;
            AssertThrows<ArgumentException>(() => command.GetBlobContent(blockBlob, fileName, false),
                String.Format(Resources.ObjectCannotBeNull, typeof(ICloudBlob).Name));
            string bloburi = "http://127.0.0.1/account/test/blob";
            blockBlob = new CloudBlockBlob(new Uri(bloburi));
            fileName = "*&^";
            AssertThrows<ArgumentException>(() => command.GetBlobContent(blockBlob, fileName, false),
                String.Format(Resources.InvalidFileName, fileName));
            bloburi = "http://127.0.0.1/account/test/blob*+";
            fileName = string.Empty;
            blockBlob = new CloudBlockBlob(new Uri(bloburi));
            AssertThrows<ArgumentException>(() => command.GetBlobContent(blockBlob, fileName, false),
                String.Format(Resources.InvalidFileName, "blob*+"));
        }

        [TestMethod]
        public void GetBlobContentByICloudBlobWithNotExistsContainerTest()
        {
            string bloburi = "http://127.0.0.1/account/test/blob0";
            CloudBlockBlob blockBlob = new CloudBlockBlob(new Uri(bloburi));
            string fileName = "abc";
            AssertThrows<ResourceNotFoundException>(() => command.GetBlobContent(blockBlob, fileName, false),
                String.Format(Resources.ContainerNotFound, "test"));
        }

        [TestMethod]
        public void GetBlobContentByICloudBlobWithNotExistsBlobTest()
        {
            AddTestContainers();
            string bloburi = "http://127.0.0.1/account/test/blob0";
            CloudBlockBlob blockBlob = new CloudBlockBlob(new Uri(bloburi));
            string fileName = "abc";
            AssertThrows<ResourceNotFoundException>(() => command.GetBlobContent(blockBlob, fileName, false),
                String.Format(Resources.BlobNotFound, "blob0", "test"));
        }

        [TestMethod]
        public void GetBlobContentByICloudBlobWithInvalidDestinationTest()
        {
            AddTestContainers();

            string bloburi = "http://127.0.0.1/account/test/blob0";
            CloudBlockBlob blockBlob = new CloudBlockBlob(new Uri(bloburi));

            string fileName = @"D:\E\xxxxx\febc\def";
            AssertThrows<ArgumentException>(() => command.GetBlobContent(blockBlob, fileName, false),
                String.Format(Resources.DirectoryNotExists, @"D:\E\xxxxx\febc"));

            fileName = @"c:\Windows\System32\cmd.exe";
            AssertThrows<ArgumentException>(() => command.GetBlobContent(blockBlob, fileName, false),
                String.Format(Resources.FileAlreadyExists, fileName));
        }

        [TestMethod]
        public void GetBlobContentByICloudBlobWithDownloadExceptionTest()
        {
            AddTestBlobs();
            string bloburi = "http://127.0.0.1/account/container1/blob0";
            string fileName = string.Empty;
            CloudBlockBlob blockBlob = new CloudBlockBlob(new Uri(bloburi));
            command.Force = true;
            command.Exception = true;
            AssertThrows<ArgumentException>(() => command.GetBlobContent(blockBlob, fileName, false), "FakeGetAzureStorageBlobContentCommand");
        }

        [TestMethod]
        public void GetBlobContentByICloudBlobSuccessfullyTest()
        {
            AddTestBlobs();
            string bloburi = "http://127.0.0.1/account/container1/blob0";
            string fileName = string.Empty;
            CloudBlockBlob blockBlob = new CloudBlockBlob(new Uri(bloburi));
            command.Force = true;
            command.Exception = false;

            AzureStorageBlob blob = command.GetBlobContent(blockBlob, fileName, false);
            Assert.AreEqual("blob0", blob.Name);

            blob = command.GetBlobContent(blockBlob, fileName, true);
            Assert.AreEqual("blob0", blob.Name);

            fileName = @"c:\Windows\System32";
            command.GetBlobContent(blockBlob, fileName, false);
        }
    }

    internal class FakeGetAzureStorageBlobContentCommand : GetAzureStorageBlobContentCommand
    {
        public bool Exception = false;

        public FakeGetAzureStorageBlobContentCommand(IStorageBlobManagement channel)
        {
            Channel = channel;
        }

        internal override void DownloadBlob(ICloudBlob blob, string filePath)
        {
            ProgressRecord pr = new ProgressRecord(0, "a", "b");
            OnStart(pr);
            OnProgress(pr, 1, 10.5);
            OnFinish(pr, null);
            if (Exception)
            {
                throw new ArgumentException("FakeGetAzureStorageBlobContentCommand");
            }
        }
    }
}
