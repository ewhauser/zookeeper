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
namespace ZooKeeperNet
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using log4net;
    using Org.Apache.Zookeeper.Data;
    using Org.Apache.Zookeeper.Proto;

    /// <inheritdoc />
    [DebuggerDisplay("Id = {Id}")]
    public class ZooKeeper : IDisposable, IZooKeeper
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(ZooKeeper));

        private readonly ZKWatchManager watchManager = new ZKWatchManager();

        public IEnumerable<string> DataWatches
        {
            get
            {
                return watchManager.dataWatches.Keys;
            }
        }

        public IEnumerable<string> ExistWatches
        {
            get
            {
                return watchManager.existWatches.Keys;
            }
        }

        public IEnumerable<string> ChildWatches
        {
            get
            {
                return watchManager.childWatches.Keys;
            }
        }

        /// <summary>
        /// Manage watchers & handle events generated by the ClientCnxn object.
        ///
        /// We are implementing this as a nested class of ZooKeeper so that
        /// the public methods will not be exposed as part of the ZooKeeper client
        /// API.
        /// </summary>
        public abstract class WatchRegistration
        {
            private readonly IWatcher watcher;
            private readonly string clientPath;

            /// <summary>
            /// Initializes a new instance of the <see cref="WatchRegistration"/> class.
            /// </summary>
            /// <param name="watcher">The watcher.</param>
            /// <param name="clientPath">The client path.</param>
            protected WatchRegistration(IWatcher watcher, string clientPath)
            {
                this.watcher = watcher;
                this.clientPath = clientPath;
            }

            abstract protected IDictionary<string, HashSet<IWatcher>> GetWatches(int rc);

            /// <summary>
            /// Register the watcher with the set of watches on path.
            /// </summary>
            /// <param name="rc">the result code of the operation that attempted to add the watch on the path.</param>
            public void Register(int rc)
            {
                if (!ShouldAddWatch(rc)) return;

                var watches = GetWatches(rc);
                HashSet<IWatcher> watchers;
                watches.TryGetValue(clientPath, out watchers);
                if (watchers == null)
                {
                    watchers = new HashSet<IWatcher>();
                    watches[clientPath] = watchers;
                }
                watchers.Add(watcher);
            }

            /// <summary>
            /// Determine whether the watch should be added based on return code.
            /// </summary>
            /// <param name="rc">the result code of the operation that attempted to add the watch on the node</param>
            /// <returns>true if the watch should be added, otw false</returns>
            protected virtual bool ShouldAddWatch(int rc)
            {
                return rc == 0;
            }
        }

        /// <summary>
        /// Handle the special case of exists watches - they add a watcher
        /// even in the case where NONODE result code is returned.
        /// </summary>
        internal class ExistsWatchRegistration : WatchRegistration
        {
            private readonly ZKWatchManager watchManager;

            public ExistsWatchRegistration(ZKWatchManager watchManager, IWatcher watcher, string clientPath)
                : base(watcher, clientPath)
            {
                this.watchManager = watchManager;
            }

            protected override IDictionary<string, HashSet<IWatcher>> GetWatches(int rc)
            {
                return rc == 0 ? watchManager.dataWatches : watchManager.existWatches;
            }

            protected override bool ShouldAddWatch(int rc)
            {
                return rc == 0 || rc == (int)KeeperException.Code.NONODE;
            }
        }

        internal class DataWatchRegistration : WatchRegistration
        {
            private readonly ZKWatchManager watchManager;

            public DataWatchRegistration(ZKWatchManager watchManager, IWatcher watcher, string clientPath)
                : base(watcher, clientPath)
            {
                this.watchManager = watchManager;
            }

            protected override IDictionary<string, HashSet<IWatcher>> GetWatches(int rc)
            {
                return watchManager.dataWatches;
            }
        }

        internal class ChildWatchRegistration : WatchRegistration
        {
            private readonly ZKWatchManager watchManager;

            public ChildWatchRegistration(ZKWatchManager watchManager, IWatcher watcher, string clientPath)
                : base(watcher, clientPath)
            {
                this.watchManager = watchManager;
            }

            protected override IDictionary<string, HashSet<IWatcher>> GetWatches(int rc)
            {
                return watchManager.childWatches;
            }
        }

        public class States : IEquatable<States>
        {
            public static readonly States CONNECTING = new States("CONNECTING");
            public static readonly States ASSOCIATING = new States("ASSOCIATING");
            public static readonly States CONNECTED = new States("CONNECTED");
            public static readonly States CLOSED = new States("CLOSED");
            public static readonly States AUTH_FAILED = new States("AUTH_FAILED");
            public static readonly States NOT_CONNECTED = new States("NOT_CONNECTED");

            private string state;

            public States(string state)
            {
                this.state = state;
            }

            public string State
            {
                get { return state; }
            }

            public bool IsAlive()
            {
                return this != CLOSED && this != AUTH_FAILED;
            }

            public bool IsConnected()
            {
                return this == CONNECTED; // || this == CONNECTEDREADONLY
            }

            public bool IsAuthFailed()
            {
                return this == AUTH_FAILED;
            }

            public bool Equals(States other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Equals(other.state, state);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                //if (obj.GetType() != typeof(States)) return false; // don't need this
                return Equals((States)obj); // when casting to States, any non-States object will be null
            }

            public override int GetHashCode()
            {
                return (state != null ? state.GetHashCode() : 0);
            }

            public override string ToString()
            {
                return this.state;
            }
        }

        private Guid id = Guid.NewGuid();
        private volatile States state = States.NOT_CONNECTED;
        private IClientConnection cnxn;

        /// <summary>
        /// To create a ZooKeeper client object, the application needs to pass a
        /// connection string containing a comma separated list of host:port pairs,
        /// each corresponding to a ZooKeeper server.
        /// 
        /// Session establishment is asynchronous. This constructor will initiate
        /// connection to the server and return immediately - potentially (usually)
        /// before the session is fully established. The watcher argument specifies
        /// the watcher that will be notified of any changes in state. This
        /// notification can come at any point before or after the constructor call
        /// has returned.
        /// 
        /// The instantiated ZooKeeper client object will pick an arbitrary server
        /// from the connectstring and attempt to connect to it. If establishment of
        /// the connection fails, another server in the connect string will be tried
        /// (the order is non-deterministic, as we random shuffle the list), until a
        /// connection is established. The client will continue attempts until the
        /// session is explicitly closed.
        /// 
        /// Added in 3.2.0: An optional "chroot" suffix may also be appended to the
        /// connection string. This will run the client commands while interpreting
        /// all paths relative to this root (similar to the unix chroot command).
        /// </summary>
        /// <param name="connectstring">
        ///            comma separated host:port pairs, each corresponding to a zk
        ///            server. e.g. "127.0.0.1:3000,127.0.0.1:3001,127.0.0.1:3002" If
        ///            the optional chroot suffix is used the example would look
        ///            like: "127.0.0.1:3000,127.0.0.1:3001,127.0.0.1:3002/app/a"
        ///            where the client would be rooted at "/app/a" and all paths
        ///            would be relative to this root - ie getting/setting/etc...
        ///            "/foo/bar" would result in operations being run on
        ///            "/app/a/foo/bar" (from the server perspective).
        /// </param>
        /// <param name="sessionTimeout">The session timeout.</param>
        /// <param name="watcher">
        ///            a watcher object which will be notified of state changes, may
        ///            also be notified for node events
        /// </param>
        public ZooKeeper(string connectstring, TimeSpan sessionTimeout, IWatcher watcher) :
            this(connectstring, sessionTimeout, watcher, null)
        {
        }

        /// <param name="saslClient">
        /// An optional object implementing the <see cref="ISaslClient"/> interface which will be used by the
        /// <see cref="ClientConnection"/> to authenticate with the server immediately after (re)connect.
        /// </param>
        public ZooKeeper(string connectstring, TimeSpan sessionTimeout, IWatcher watcher, ISaslClient saslClient)
        {
            LOG.InfoFormat("Initiating client connection, connectstring={0} sessionTimeout={1} watcher={2}", connectstring, sessionTimeout, watcher);

            watchManager.DefaultWatcher = watcher;
            cnxn = new ClientConnection(connectstring, sessionTimeout, this, watchManager, saslClient);
            cnxn.Start();
        }

        public ZooKeeper(string connectstring, TimeSpan sessionTimeout, IWatcher watcher, long sessionId, byte[] sessionPasswd) :
            this(connectstring, sessionTimeout, watcher, null, sessionId, sessionPasswd)
        {
        }

        public ZooKeeper(string connectstring, TimeSpan sessionTimeout, IWatcher watcher, ISaslClient saslClient, long sessionId, byte[] sessionPasswd)
        {
            LOG.InfoFormat("Initiating client connection, connectstring={0} sessionTimeout={1} watcher={2} sessionId={3} sessionPasswd={4}", connectstring, sessionTimeout, watcher, sessionId, (sessionPasswd == null ? "<null>" : "<hidden>"));

            watchManager.DefaultWatcher = watcher;
            cnxn = new ClientConnection(connectstring, sessionTimeout, this, watchManager, saslClient, sessionId, sessionPasswd);
            cnxn.Start();
        }

        public Guid Id
        {
            get { return id; }
            set { id = value; }
        }

        public long SessionId
        {
            get
            {
                if (null != cnxn)
                {
                    return cnxn.SessionId;
                }
                return 0;
            }
        }

        public byte[] SesionPassword
        {
            get
            {
                return cnxn.SessionPassword;
            }
        }

        public TimeSpan SessionTimeout
        {
            get
            {
                return cnxn.SessionTimeout;
            }
        }

        public void AddAuthInfo(string scheme, byte[] auth)
        {
            cnxn.AddAuthInfo(scheme, auth);
        }

        public void Register(IWatcher watcher) // the defaultwatcher is already a full fenced so we don't need to mark the method synchronized
        {
            watchManager.DefaultWatcher = watcher;
        }

        public States State
        {
            get
            {
                return Interlocked.CompareExchange(ref state, null, null);
            }
            internal set
            {
                Interlocked.Exchange(ref state, value);
            }
        }

        /// <summary>
        /// Close this client object. Once the client is closed, its session becomes
        /// invalid. All the ephemeral nodes in the ZooKeeper server associated with
        /// the session will be removed. The watches left on those nodes (and on
        /// their parents) will be triggered.
        /// </summary>   
        private void InternalDispose()
        {
            //Assume an unitialized connection state could still require a connection disposal
            var connectionState = State;
            if (null != connectionState && !connectionState.IsAlive())
            {
                if (LOG.IsDebugEnabled)
                {
                    LOG.Debug("Close called on already closed client");
                }
                return;
            }

            if (LOG.IsDebugEnabled)
            {
                LOG.DebugFormat("Closing session: 0x{0:X}", SessionId);
            }

            try
            {
                cnxn.Dispose();
            }
            catch (Exception e)
            {
                if (LOG.IsDebugEnabled)
                {
                    LOG.Debug("Ignoring unexpected exception during close", e);
                }
            }

            LOG.DebugFormat("Session: 0x{0:X} closed", SessionId);
        }

        public void Dispose()
        {
            InternalDispose();
            GC.SuppressFinalize(this);
        }

        ~ZooKeeper()
        {
            InternalDispose();
        }

        /// <summary>
        /// Prepend the chroot to the client path (if present). The expectation of
        /// this function is that the client path has been validated before this
        /// function is called
        /// </summary>
        /// <param name="clientPath">The path to the node.</param>
        /// <returns>server view of the path (chroot prepended to client path)</returns>
        private string PrependChroot(string clientPath)
        {
            if (cnxn.ChrootPath != null)
            {
                // handle clientPath = "/"
                return clientPath.Length == 1 ? cnxn.ChrootPath : cnxn.ChrootPath + clientPath;
            }
            return clientPath;
        }

        private async Task<string> CreateAsyncInternal(string path, byte[] data, IEnumerable<ACL> acl, CreateMode createMode, bool sync)
        {
            string clientPath = path;
            PathUtils.ValidatePath(clientPath, createMode.Sequential);
            if (acl != null && acl.Count() == 0)
            {
                throw new KeeperException.InvalidACLException();
            }

            string serverPath = PrependChroot(clientPath);

            RequestHeader h = new RequestHeader();
            h.Type = (int)OpCode.Create;
            CreateRequest request = new CreateRequest(serverPath, data, acl, createMode.Flag);
            CreateResponse response = new CreateResponse();
            ReplyHeader r = sync ? cnxn.SubmitRequest(h, request, response, null)
                : await cnxn.SubmitRequestAsync(h, request, response, null).ConfigureAwait(false);
            if (r.Err != 0)
            {
                throw KeeperException.Create((KeeperException.Code)Enum.ToObject(typeof(KeeperException.Code), r.Err), clientPath);
            }
            return cnxn.ChrootPath == null ? response.Path : response.Path.Substring(cnxn.ChrootPath.Length);
        }

        public string Create(string path, byte[] data, IEnumerable<ACL> acl, CreateMode createMode)
        {
            return CreateAsyncInternal(path, data, acl, createMode, sync: true).GetAwaiter().GetResult();
        }

        public Task<string> CreateAsync(string path, byte[] data, IEnumerable<ACL> acl, CreateMode createMode)
        {
            return CreateAsyncInternal(path, data, acl, createMode, sync: false);
        }

        private async Task DeleteAsyncInternal(string path, int version, bool sync)
        {
            string clientPath = path;
            PathUtils.ValidatePath(clientPath);

            string serverPath;

            // maintain semantics even in chroot case
            // specifically - root cannot be deleted
            // I think this makes sense even in chroot case.
            if (clientPath.Equals(PathUtils.PathSeparator))
            {
                // a bit of a hack, but delete(/) will never succeed and ensures
                // that the same semantics are maintained
                serverPath = clientPath;
            }
            else
            {
                serverPath = PrependChroot(clientPath);
            }

            RequestHeader h = new RequestHeader();
            h.Type = (int)OpCode.Delete;
            DeleteRequest request = new DeleteRequest(serverPath, version);
            ReplyHeader r = sync ? cnxn.SubmitRequest(h, request, null, null)
                : await cnxn.SubmitRequestAsync(h, request, null, null).ConfigureAwait(false);
            if (r.Err != 0)
            {
                throw KeeperException.Create((KeeperException.Code)Enum.ToObject(typeof(KeeperException.Code), r.Err), clientPath);
            }
        }

        public void Delete(string path, int version)
        {
            DeleteAsyncInternal(path, version, sync: true).GetAwaiter().GetResult();
        }

        public Task DeleteAsync(string path, int version)
        {
            return DeleteAsyncInternal(path, version, sync: false);
        }

        private async Task<Stat> ExistsAsyncInternal(string path, IWatcher watcher, bool sync)
        {
            string clientPath = path;
            PathUtils.ValidatePath(clientPath);

            // the watch contains the un-chroot path
            WatchRegistration wcb = null;
            if (watcher != null)
            {
                wcb = new ExistsWatchRegistration(watchManager, watcher, clientPath);
            }

            string serverPath = PrependChroot(clientPath);

            RequestHeader h = new RequestHeader();
            h.Type = (int)OpCode.Exists;
            ExistsRequest request = new ExistsRequest(serverPath, watcher != null);
            SetDataResponse response = new SetDataResponse();
            ReplyHeader r = sync ? cnxn.SubmitRequest(h, request, response, wcb)
                : await cnxn.SubmitRequestAsync(h, request, response, wcb).ConfigureAwait(false);
            if (r.Err != 0)
            {
                if (r.Err == (int)KeeperException.Code.NONODE)
                {
                    return null;
                }
                throw KeeperException.Create((KeeperException.Code)Enum.ToObject(typeof(KeeperException.Code), r.Err), clientPath);
            }

            return response.Stat.Czxid == -1 ? null : response.Stat;
        }

        public Stat Exists(string path, IWatcher watcher)
        {
            return ExistsAsyncInternal(path, watcher, sync: true).GetAwaiter().GetResult();
        }

        public Task<Stat> ExistsAsync(string path, IWatcher watcher)
        {
            return ExistsAsyncInternal(path, watcher, sync: false);
        }

        public Stat Exists(string path, bool watch)
        {
            return Exists(path, watch ? watchManager.DefaultWatcher : null);
        }

        public Task<Stat> ExistsAsync(string path, bool watch)
        {
            return ExistsAsync(path, watch ? watchManager.DefaultWatcher : null);
        }

        private async Task<byte[]> GetDataAsyncInternal(string path, IWatcher watcher, Stat stat, bool sync)
        {
            string clientPath = path;
            PathUtils.ValidatePath(clientPath);

            // the watch contains the un-chroot path
            WatchRegistration wcb = null;
            if (watcher != null)
            {
                wcb = new DataWatchRegistration(watchManager, watcher, clientPath);
            }

            string serverPath = PrependChroot(clientPath);

            RequestHeader h = new RequestHeader();
            h.Type = (int)OpCode.GetData;
            GetDataRequest request = new GetDataRequest(serverPath, watcher != null);
            GetDataResponse response = new GetDataResponse();
            ReplyHeader r = sync ? cnxn.SubmitRequest(h, request, response, wcb)
                : await cnxn.SubmitRequestAsync(h, request, response, wcb).ConfigureAwait(false);
            if (r.Err != 0)
            {
                throw KeeperException.Create((KeeperException.Code)Enum.ToObject(typeof(KeeperException.Code), r.Err), clientPath);
            }
            if (stat != null)
            {
                DataTree.CopyStat(response.Stat, stat);
            }
            return response.Data;
        }

        public byte[] GetData(string path, IWatcher watcher, Stat stat)
        {
            return GetDataAsyncInternal(path, watcher, stat, sync: true).GetAwaiter().GetResult();
        }

        public Task<byte[]> GetDataAsync(string path, IWatcher watcher, Stat stat)
        {
            return GetDataAsyncInternal(path, watcher, stat, sync: false);
        }

        public byte[] GetData(string path, bool watch, Stat stat)
        {
            return GetData(path, watch ? watchManager.DefaultWatcher : null, stat);
        }

        public Task<byte[]> GetDataAsync(string path, bool watch, Stat stat)
        {
            return GetDataAsync(path, watch ? watchManager.DefaultWatcher : null, stat);
        }

        private async Task<Stat> SetDataAsyncInternal(string path, byte[] data, int version, bool sync)
        {
            string clientPath = path;
            PathUtils.ValidatePath(clientPath);

            string serverPath = PrependChroot(clientPath);

            RequestHeader h = new RequestHeader();
            h.Type = (int)OpCode.SetData;
            SetDataRequest request = new SetDataRequest(serverPath, data, version);
            SetDataResponse response = new SetDataResponse();
            ReplyHeader r = cnxn.SubmitRequest(h, request, response, null);
            if (r.Err != 0)
            {
                throw KeeperException.Create((KeeperException.Code)Enum.ToObject(typeof(KeeperException.Code), r.Err), clientPath);
            }
            return response.Stat;
        }

        public Stat SetData(string path, byte[] data, int version)
        {
            return SetDataAsyncInternal(path, data, version, sync: true).GetAwaiter().GetResult();
        }

        public Task<Stat> SetDataAsync(string path, byte[] data, int version)
        {
            return SetDataAsyncInternal(path, data, version, sync: false);
        }

        private async Task<IEnumerable<ACL>> GetACLAsyncInternal(string path, Stat stat, bool sync)
        {
            string clientPath = path;
            PathUtils.ValidatePath(clientPath);

            string serverPath = PrependChroot(clientPath);

            RequestHeader h = new RequestHeader();
            h.Type = (int)OpCode.GetACL;
            GetACLRequest request = new GetACLRequest(serverPath);
            GetACLResponse response = new GetACLResponse();
            ReplyHeader r = sync ? cnxn.SubmitRequest(h, request, response, null)
                : await cnxn.SubmitRequestAsync(h, request, response, null).ConfigureAwait(false);
            if (r.Err != 0)
            {
                throw KeeperException.Create((KeeperException.Code)Enum.ToObject(typeof(KeeperException.Code), r.Err), clientPath);
            }
            DataTree.CopyStat(response.Stat, stat);
            return response.Acl;
        }

        public IEnumerable<ACL> GetACL(string path, Stat stat)
        {
            return GetACLAsyncInternal(path, stat, sync: true).GetAwaiter().GetResult();
        }

        public Task<IEnumerable<ACL>> GetACLAsync(string path, Stat stat)
        {
            return GetACLAsyncInternal(path, stat, sync: false);
        }

        private async Task<Stat> SetACLAsyncInternal(string path, IEnumerable<ACL> acl, int version, bool sync)
        {
            string clientPath = path;
            PathUtils.ValidatePath(clientPath);
            if (acl != null && acl.Count() == 0)
            {
                throw new KeeperException.InvalidACLException();
            }

            string serverPath = PrependChroot(clientPath);

            RequestHeader h = new RequestHeader();
            h.Type = (int)OpCode.SetACL;
            SetACLRequest request = new SetACLRequest(serverPath, acl, version);
            SetACLResponse response = new SetACLResponse();
            ReplyHeader r = sync ? cnxn.SubmitRequest(h, request, response, null)
                : await cnxn.SubmitRequestAsync(h, request, response, null).ConfigureAwait(false);
            if (r.Err != 0)
            {
                throw KeeperException.Create((KeeperException.Code)Enum.ToObject(typeof(KeeperException.Code), r.Err), clientPath);
            }
            return response.Stat;
        }

        public Stat SetACL(string path, IEnumerable<ACL> acl, int version)
        {
            return SetACLAsyncInternal(path, acl, version, sync: true).GetAwaiter().GetResult();
        }

        public Task<Stat> SetACLAsync(string path, IEnumerable<ACL> acl, int version)
        {
            return SetACLAsyncInternal(path, acl, version, sync: false);
        }

        private async Task<IEnumerable<string>> GetChildrenAsyncInternal(string path, IWatcher watcher, bool sync)
        {
            string clientPath = path;
            PathUtils.ValidatePath(clientPath);

            // the watch contains the un-chroot path
            WatchRegistration wcb = null;
            if (watcher != null)
            {
                wcb = new ChildWatchRegistration(watchManager, watcher, clientPath);
            }

            string serverPath = PrependChroot(clientPath);

            RequestHeader h = new RequestHeader();
            h.Type = (int)OpCode.GetChildren2;
            GetChildren2Request request = new GetChildren2Request(serverPath, watcher != null);
            GetChildren2Response response = new GetChildren2Response();
            ReplyHeader r = sync ? cnxn.SubmitRequest(h, request, response, wcb)
                : await cnxn.SubmitRequestAsync(h, request, response, wcb).ConfigureAwait(false);
            if (r.Err != 0)
            {
                throw KeeperException.Create((KeeperException.Code)Enum.ToObject(typeof(KeeperException.Code), r.Err), clientPath);
            }
            return response.Children;
        }

        public IEnumerable<string> GetChildren(string path, IWatcher watcher)
        {
            return GetChildrenAsyncInternal(path, watcher, sync: true).GetAwaiter().GetResult();
        }

        public Task<IEnumerable<string>> GetChildrenAsync(string path, IWatcher watcher)
        {
            return GetChildrenAsyncInternal(path, watcher, sync: false);
        }

        public IEnumerable<string> GetChildren(string path, bool watch)
        {
            return GetChildren(path, watch ? watchManager.DefaultWatcher : null);
        }

        public Task<IEnumerable<string>> GetChildrenAsync(string path, bool watch)
        {
            return GetChildrenAsync(path, watch ? watchManager.DefaultWatcher : null);
        }

        private async Task<IEnumerable<string>> GetChildrenAsyncInternal(string path, IWatcher watcher, Stat stat, bool sync)
        {
            string clientPath = path;
            PathUtils.ValidatePath(clientPath);

            // the watch contains the un-chroot path
            WatchRegistration wcb = null;
            if (watcher != null)
            {
                wcb = new ChildWatchRegistration(watchManager, watcher, clientPath);
            }

            string serverPath = PrependChroot(clientPath);

            RequestHeader h = new RequestHeader();
            h.Type = (int)OpCode.GetChildren2;
            GetChildren2Request request = new GetChildren2Request(serverPath, watcher != null);
            GetChildren2Response response = new GetChildren2Response();
            ReplyHeader r = sync ? cnxn.SubmitRequest(h, request, response, wcb)
                : await cnxn.SubmitRequestAsync(h, request, response, wcb).ConfigureAwait(false);
            if (r.Err != 0)
            {
                throw KeeperException.Create((KeeperException.Code)Enum.ToObject(typeof(KeeperException.Code), r.Err), clientPath);
            }
            if (stat != null)
            {
                DataTree.CopyStat(response.Stat, stat);
            }
            return response.Children;
        }

        public IEnumerable<string> GetChildren(string path, IWatcher watcher, Stat stat)
        {
            return GetChildrenAsyncInternal(path, watcher, stat, sync: true).GetAwaiter().GetResult();
        }

        public Task<IEnumerable<string>> GetChildrenAsync(string path, IWatcher watcher, Stat stat)
        {
            return GetChildrenAsyncInternal(path, watcher, stat, sync: false);
        }

        public IEnumerable<string> GetChildren(string path, bool watch, Stat stat)
        {
            return GetChildren(path, watch ? watchManager.DefaultWatcher : null, stat);
        }

        public Task<IEnumerable<string>> GetChildrenAsync(string path, bool watch, Stat stat)
        {
            return GetChildrenAsync(path, watch ? watchManager.DefaultWatcher : null, stat);
        }

        /// <summary>
        /// string representation of this ZooKeeper client. Suitable for things
        /// like logging.
        /// 
        /// Do NOT count on the format of this string, it may change without
        /// warning.
        /// 
        /// @since 3.3.0
        /// </summary>
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder("Id: ")
                .Append(id)
                .Append(" State:")
                .Append(State);
            if (State == States.CONNECTED)
                builder.Append(" Timeout:")
                    .Append(SessionTimeout);
            builder.Append(" ")
                .Append(cnxn);
            return builder.ToString();
        }
    }
}
