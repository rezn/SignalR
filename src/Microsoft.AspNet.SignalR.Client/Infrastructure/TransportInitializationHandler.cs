// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.md in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client.Http;
using Microsoft.AspNet.SignalR.Client.Transports;
using Microsoft.AspNet.SignalR.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNet.SignalR.Client.Infrastructure
{
    public class TransportInitializationHandler
    {
        private ThreadSafeInvoker _initializationInvoker;
        private TaskCompletionSource<object> _initializationTask;
        private IConnection _connection;
        private IHttpClient _httpClient;
        private string _connectionData;
        private IDisposable _tokenCleanup;

        public TransportInitializationHandler(IConnection connection,
                                              IHttpClient httpClient,
                                              string connectionData,
                                              CancellationToken disconnectToken)
        {
            _httpClient = httpClient;
            _connection = connection;
            _connectionData = connectionData;

            _initializationTask = new TaskCompletionSource<object>();
            _initializationInvoker = new ThreadSafeInvoker();

            // Default event
            OnFailure = () => { };

            // We want to fail if the disconnect token is tripped while we're waiting on initialization
            _tokenCleanup = disconnectToken.SafeRegister(_ =>
            {
                Fail();
            },
            state: null);

            TaskAsyncHelper.Delay(connection.TotalTransportConnectTimeout).Then(() =>
            {
                Fail(new TimeoutException(Resources.Error_TransportTimedOutTryingToConnect));
            });
        }

        public event Action OnFailure;

        public Task Task
        {
            get
            {
                return _initializationTask.Task;
            }
        }

        public void Success()
        {
            _httpClient.GetStartResponse(_connection, _connectionData).Then(response =>
            {
                var started = _connection.JsonDeserializeObject<JObject>(response)["Response"];
                if (started.ToString() == "started")
                {
                    SuccessComplete();
                }
                else
                {
                    Fail(new InvalidOperationException(Resources.Error_StartFailed));
                }
            }).Catch(ex =>
            {
                Fail(new InvalidOperationException(Resources.Error_StartFailed, ex));
            });
        }

        public void Fail()
        {
            Fail(new InvalidOperationException(Resources.Error_TransportFailedToConnect));
        }

        public void Fail(Exception ex)
        {
            _initializationInvoker.Invoke(() =>
            {
                OnFailure();
                _initializationTask.SetException(ex);
                _tokenCleanup.Dispose();
            });
        }

        private void SuccessComplete()
        {
            _initializationInvoker.Invoke(() =>
            {
#if NETFX_CORE
                Task.Run(() =>
#else
                ThreadPool.QueueUserWorkItem(_ =>
#endif
                {
                    _initializationTask.SetResult(null);
                });

                _tokenCleanup.Dispose();
            });
        }
    }
}
