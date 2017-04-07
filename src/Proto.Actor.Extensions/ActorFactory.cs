﻿using System;
using Microsoft.Extensions.DependencyInjection;

namespace Proto
{
    public class ActorFactory : IActorFactory
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ActorPropsRegistry actorPropsRegistry;

        public ActorFactory(IServiceProvider serviceProvider, ActorPropsRegistry actorPropsRegistry)
        {
            this.serviceProvider = serviceProvider;
            this.actorPropsRegistry = actorPropsRegistry;
        }

        public PID GetActor<T>(T actor, string id, string address = null, IContext parent = null)
            where T : IActor
        {
            id = id ?? typeof(T).FullName;
            address = address ?? ProcessRegistry.NoHost;

            var pid = new PID(address, id);
            var reff = ProcessRegistry.Instance.Get(pid);
            if (reff is DeadLetterProcess)
            {
                pid = CreateActor<T>(id, parent, () => new Props().WithProducer(() => actor));
            }
            return pid;
        }
        
        public PID GetActor<T>(string id = null, string address = null, IContext parent = null)
            where T : IActor
        {
            id = id ?? typeof(T).FullName;
            address = address ?? ProcessRegistry.NoHost;

            var pidId = id;
            if (parent != null)
            {
                pidId = $"{parent.Self.Id}/{id}";
            }

            var pid = new PID(address, pidId);
            var reff = ProcessRegistry.Instance.Get(pid);
            if (reff is DeadLetterProcess)
            {
                pid = CreateActor<T>(id, parent, () => new Props().WithProducer(() => ActivatorUtilities.CreateInstance<T>(serviceProvider)));
            }
            return pid;
        }

        private PID CreateActor<T>(string id, IContext parent, Func<Props> producer)
            where T : IActor
        {
            Func<Props, Props> props;
            if (!actorPropsRegistry.RegisteredProps.TryGetValue(typeof(T), out props))
            {
                props = x => x;
            }

            var props2 = props(producer());
            if (parent == null)
            {
                return Actor.SpawnNamed(props2, id);
            }
            return parent.SpawnNamed(props2, id);
        }
    }
}