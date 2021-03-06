﻿// -----------------------------------------------------------------------
//   <copyright file="EndpointWriterMailbox.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using Proto.Mailbox;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    internal static class MailboxStatus
    {
        public const int Idle = 0;
        public const int Busy = 1;
    }

    public class EndpointWriterMailbox : IMailbox
    {
        private static readonly ILogger Logger = Log.CreateLogger<EndpointWriterMailbox>();
        private readonly int _batchSize;
        private readonly IMailboxQueue _systemMessages = new UnboundedMailboxQueue();
        private readonly IMailboxQueue _userMessages = new UnboundedMailboxQueue();
        private IDispatcher _dispatcher;
        private IMessageInvoker _invoker;

        private int _status = MailboxStatus.Idle;
        private bool _suspended;

        public EndpointWriterMailbox(int batchSize)
        {
            _batchSize = batchSize;
        }

        public void PostUserMessage(object msg)
        {
            _userMessages.Push(msg);
            Schedule();
        }

        public void PostSystemMessage(object msg)
        {
            _systemMessages.Push(msg);
            Schedule();
        }

        public void RegisterHandlers(IMessageInvoker invoker, IDispatcher dispatcher)
        {
            _invoker = invoker;
            _dispatcher = dispatcher;
        }

        public void Start()
        {
        }

        private async Task RunAsync()
        {
            object m = null;
            try
            {
                var _ = _dispatcher.Throughput; //not used for batch mailbox
                var batch = new List<RemoteDeliver>(_batchSize);
                var sys = _systemMessages.Pop();
                if (sys != null)
                {
                    if (sys is SuspendMailbox)
                    {
                        _suspended = true;
                    }
                    if (sys is ResumeMailbox)
                    {
                        _suspended = false;
                    }
                    m = sys;
                    await _invoker.InvokeSystemMessageAsync(sys);
                }
                if (!_suspended)
                {
                    batch.Clear();
                    object msg;
                    while ((msg = _userMessages.Pop()) != null)
                    {
                        if (msg is EndpointTerminatedEvent) //The mailbox was crashing when it received this particular message 
                        {
                            await _invoker.InvokeUserMessageAsync(msg);
                            continue;
                        }

                        batch.Add((RemoteDeliver) msg);
                        if (batch.Count >= _batchSize)
                        {
                            break;
                        }
                    }

                    if (batch.Count > 0)
                    {
                        m = batch;
                        await _invoker.InvokeUserMessageAsync(batch);
                    }
                }

            }
            catch (Exception x)
            {
                Logger.LogWarning("Exception in RunAsync", x);
                _invoker.EscalateFailure(x,m);
                return; //Without this, messages are delivered while failure is being escalated
            }


            Interlocked.Exchange(ref _status, MailboxStatus.Idle);

            if (_userMessages.HasMessages || _systemMessages.HasMessages &! _suspended )
            {
                Schedule();
            }
        }

        protected void Schedule()
        {
           
            if (Interlocked.CompareExchange(ref _status, MailboxStatus.Busy, MailboxStatus.Idle) == MailboxStatus.Idle)
            {
                _dispatcher.Schedule(RunAsync);
            }
        }
    }
}