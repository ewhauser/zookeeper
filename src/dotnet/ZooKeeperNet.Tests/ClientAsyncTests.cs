/*
 *  Licensed to the Apache Software Foundation (ASF) under one or more
 *  contributor license agreements.  See the NOTICE file distributed with
 *  this work for additional information regarding copyright ownership.
 *  The ASF licenses this file to You under the Apache License, Version 2.0
 *  (the "License"); you may not use this file except in compliance with
 *  the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 *
 */
namespace ZooKeeperNet.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Org.Apache.Zookeeper.Data;
    using static ZooKeeperNet.KeeperException;

    [TestFixture]
    public class ClientAsyncTests : AbstractZooKeeperTests
    {
        [Test]
        public async Task CreateAndDeleteTest()
        {
            string path = $"/async{Guid.NewGuid()}";

            using (ZooKeeper zk = CreateClient())
            {
                await zk.CreateAsync(path, new byte[0], Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent);
                Assert.That(await zk.ExistsAsync(path, false), Is.Not.Null);
                await zk.DeleteAsync(path, -1);
                Assert.That(await zk.ExistsAsync(path, false), Is.Null);
            }
        }

        [Test]
        public async Task SetAndGetDataTest()
        {
            string path = $"/asyncdata{Guid.NewGuid()}";

            using (ZooKeeper zk = CreateClient())
            {
                await zk.CreateAsync(path, new byte[0], Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent);
                Assert.That(await zk.GetDataAsync(path, false, new Stat()), Is.Empty);

                byte[] data = "async data".GetBytes();
                await zk.SetDataAsync(path, data, -1);
                Assert.That(await zk.GetDataAsync(path, false, new Stat()), Is.EqualTo(data));
            }
        }

        [Test]
        public async Task SetAndGetACLTest()
        {
            string path = $"/asyncacl{Guid.NewGuid()}";

            using (ZooKeeper zk = CreateClient())
            {
                await zk.CreateAsync(path, new byte[0], Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent);
                Assert.That(await zk.GetACLAsync(path, new Stat()), Is.EquivalentTo(Ids.OPEN_ACL_UNSAFE));

                await zk.SetACLAsync(path, Ids.READ_ACL_UNSAFE, -1);
                Assert.That(await zk.GetACLAsync(path, new Stat()), Is.EquivalentTo(Ids.READ_ACL_UNSAFE));
            }
        }

        [Test]
        public async Task GetChildrenTest()
        {
            string path = $"/asyncchildren{Guid.NewGuid()}";
            string child1path = "child1";
            string child2path = "child2";

            using (ZooKeeper zk = CreateClient())
            {
                await zk.CreateAsync(path, new byte[0], Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent);
                Assert.That(await zk.GetChildrenAsync(path, false), Is.Empty);

                await Task.WhenAll(zk.CreateAsync($"{path}/{child1path}", new byte[0], Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent),
                    zk.CreateAsync($"{path}/{child2path}", new byte[0], Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent)).ConfigureAwait(false);

                Assert.That(await zk.GetChildrenAsync(path, false), Is.EquivalentTo(new[] { child1path, child2path }));
            }
        }

        [Test]
        public async Task ExceptionHandlingTest()
        {
            string path = $"/asyncexception{Guid.NewGuid()}";

            using (ZooKeeper zk = CreateClient())
            {
                await zk.CreateAsync(path, new byte[0], Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent);
                Assert.That(async () => await zk.CreateAsync(path, new byte[0], Ids.OPEN_ACL_UNSAFE, CreateMode.Persistent),
                    Throws.Exception.TypeOf<NodeExistsException>().With.Property(nameof(NodeExistsException.ErrorCode)).EqualTo(Code.NODEEXISTS));
            }
        }

        [Test]
        public void TimeoutExceptionTest()
        {
            string path = $"/asynctimeout{Guid.NewGuid()}";

            using (ZooKeeper zk = CreateClient(new TimeSpan(1)))
            {
                Assert.That(async () => await zk.ExistsAsync(path, false), Throws.Exception.TypeOf<TimeoutException>());
            }
        }
    }
}
