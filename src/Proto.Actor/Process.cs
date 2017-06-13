// -----------------------------------------------------------------------
//  <copyright file="Process.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto
{
    public abstract class Process
    {
        public abstract Task SendUserMessageAsync(PID pid, object message);

        public virtual async Task StopAsync(PID pid)
        {
            await SendSystemMessageAsync(pid, new Stop());
        }

        public abstract Task SendSystemMessageAsync(PID pid, object message);
    }

    public class LocalProcess : Process
    {
        private long _isDead;
        public IMailbox Mailbox { get; }

        internal bool IsDead
        {
            get => Interlocked.Read(ref _isDead) == 1;
            private set => Interlocked.Exchange(ref _isDead, value ? 1 : 0);
        }

        public LocalProcess(IMailbox mailbox)
        {
            Mailbox = mailbox;
        }


        public override async Task SendUserMessageAsync(PID pid, object message)
        {
            await Mailbox.PostUserMessageAsync(message);
        }

        public override async Task SendSystemMessageAsync(PID pid, object message)
        {
            await Mailbox.PostSystemMessageAsync(message);
        }

        public override async Task StopAsync(PID pid)
        {
            await base.StopAsync(pid);
            IsDead = true;
        }
    }
}