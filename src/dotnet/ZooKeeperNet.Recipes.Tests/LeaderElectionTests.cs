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

using System;
using System.Threading;
using NUnit.Framework;
using ZooKeeperNet;
using ZooKeeperNet.Recipes;
using ZooKeeperNet.Tests;
using log4net;

namespace ZooKeeperNetRecipes.Tests {
	[TestFixture]
	public class LeaderElectionTests : AbstractZooKeeperTests {
		private static ILog LOG = LogManager.GetLogger(typeof (LeaderElectionTests));
		private ZooKeeper[] clients;

		[TearDown]
		public void Teardown() {
			foreach (var zk in clients)
				zk.Dispose();
		}

		private class TestLeaderWatcher : ILeaderWatcher {
			public static byte Leader;
			private readonly byte b;

			public TestLeaderWatcher(byte b) {
				this.b = b;
			}

			public void TakeLeadership() {
				Leader = b;
				LOG.DebugFormat("Leader: {0:x}", b);
			}
		}

		[Test]
		public void testElection() {
			String dir = "/test";
			String testString = "Hello World";
			int num_clients = 10;
			clients = new ZooKeeper[num_clients];
			LeaderElection[] elections = new LeaderElection[num_clients];
			for (byte i = 0; i < clients.Length; i++) {
				clients[i] = CreateClient();
				elections[i] = new LeaderElection(clients[i], dir, new TestLeaderWatcher(i), new[] {i});
				elections[i].Start();
			}

			for (byte i = 0; i < clients.Length; i++) {
				while (!elections[i].IsOwner) {
					Thread.Sleep(1);
				}
				elections[i].Close();
			}
			Assert.Pass();
		}
	}
}
