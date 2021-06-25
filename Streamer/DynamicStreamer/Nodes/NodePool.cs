using DynamicStreamer.Queues;
using System;
using System.Collections.Generic;

namespace DynamicStreamer.Nodes
{
    public class NodePool<TContext, TContextConfig, TInput, TOutput> : IDisposable, IRuntimeItem, ISourceQueueHolder where TContext : IDisposable
    {
        private readonly Stack<Node<TContext, TContextConfig, TInput, TOutput>> _runtimeNodes;
        private readonly List<Node<TContext, TContextConfig, TInput, TOutput>> _nodes;
        private readonly List<Node<TContext, TContextConfig, TInput, TOutput>> _nodesToRemove = new List<Node<TContext, TContextConfig, TInput, TOutput>>();

        private readonly IStreamerBase _controller;
        private readonly Func<int, Node<TContext, TContextConfig, TInput, TOutput>> _creator;

        public ISourceQueue<TInput> InputQueue { get; set; }

        public NodeName Name { get; }

        public ISourceQueue InputQueueForOverload => InputQueue;

        public NodePool(NodeName name, IStreamerBase controller, Func<int, Node<TContext, TContextConfig, TInput, TOutput>> creator)
        {
            Name = name;
            _nodes = new List<Node<TContext, TContextConfig, TInput, TOutput>>();
            _runtimeNodes = new Stack<Node<TContext, TContextConfig, TInput, TOutput>>();
            _controller = controller;
            _creator = creator;
        }

        public bool TryGet(out Node<TContext, TContextConfig, TInput, TOutput> node)
        {
            lock (this)
            {
                return _runtimeNodes.TryPop(out node);
            }
        }

        public void Back(Node<TContext, TContextConfig, TInput, TOutput> node)
        {
            lock (this)
            {
                if (_nodesToRemove.Count > 0 && _nodesToRemove.Contains(node))
                {
                    _nodesToRemove.Remove(node);
                    node.Dispose();
                    return;
                }
                _runtimeNodes.Push(node);
            }
        }

        public void ActivateFromPool()
        {
            if (TryGet(out var node))
            {
                if (InputQueue.TryDequeue(out var data))
                {
                    _controller.ProcessingPool.Enqueue(new ProcessingItem(() =>
                    {
                        node.ProcessData(data);
                        Back(node);

                        ActivateFromPool();
                    }));
                }
                else
                    Back(node);
            }
        }

        public TContext PrepareVersion(UpdateVersionContext version, int instances, ISourceQueue<TInput> inputQueue, Func<UpdateVersionContext, Node<TContext, TContextConfig, TInput, TOutput>, TContext> prepareContext)
        {
            version.RuntimeConfig.Add(this, null);

            var pendingNodesToAdd = new List<Node<TContext, TContextConfig, TInput, TOutput>>();

            if (_nodes.Count != instances)
                Core.LogInfo($"Changing number of pooled nodes for {Name} from {_nodes.Count} to {instances}");

            while (_nodes.Count + pendingNodesToAdd.Count < instances)
            {
                var newNode = _creator(_nodes.Count + pendingNodesToAdd.Count);
                pendingNodesToAdd.Add(newNode);
            }

            TContext result = default;
            foreach (var item in _nodes)
                result = prepareContext(version, item);

            foreach (var item in pendingNodesToAdd)
                result = prepareContext(version, item);

            version.AddDeploy(() =>
            {
                lock (this)
                {
                    InputQueue = inputQueue;
                    InputQueue.OnChanged = ActivateFromPool;

                    pendingNodesToAdd.ForEach(s => _runtimeNodes.Push(s));
                    _nodes.AddRange(pendingNodesToAdd);
                    while (_nodes.Count > instances)
                    {
                        _nodesToRemove.Add(_nodes[0]);
                        _nodes.RemoveAt(0);
                    }
                }
            });

            return result;
        }

        public void Dispose()
        {
            bool again = false;
            lock (this)
            {
                while (_runtimeNodes.TryPop(out var node))
                {
                    _nodes.Remove(node);
                    node.Dispose();
                }
                again = _nodes.Count > 0;
            }

            if (again)
                _controller.AddPendingDisposal(Dispose);
        }
    }
}
