using System;
using System.Collections.Generic;
using System.Diagnostics;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Logging;

namespace Nethermind.Trie.Pruning
{
    // is this class even needed?
    // we need to stack ref changes for each block
    public class TreeCommitter : ITreeCommitter
    {
        public TreeCommitter(
            IKeyValueStore keyValueStore,
            ILogManager logManager,
            long memoryLimit,
            long lookupLimit = 128)
        {
            // ReSharper disable once ConstantConditionalAccessQualifier
            _logger = logManager?.GetClassLogger() ?? throw new ArgumentNullException(nameof(logManager));
            _keyValueStore = keyValueStore ?? throw new ArgumentNullException(nameof(keyValueStore));

            if (_logger.IsTrace)
                _logger.Trace($"Creating a new {nameof(TreeCommitter)} with memory limit {memoryLimit}");

            if (memoryLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(memoryLimit));

            _memoryLimit = memoryLimit;
            MemorySize =
                MemorySizes.SmallObjectOverhead +
                5 * MemorySizes.RefSize -
                MemorySizes.SmallObjectFreeDataSize +
                40 /* linked list */;
        }

        public void Commit(long blockNumber, TrieNode? trieNode)
        {
            if (_logger.IsTrace)
                _logger.Trace($"Committing {blockNumber} {trieNode}");

            if (blockNumber < 0)
                throw new ArgumentOutOfRangeException(nameof(blockNumber));

            bool shouldBeginNewPackage = CurrentPackage == null || CurrentPackage.BlockNumber != blockNumber;
            if (shouldBeginNewPackage)
            {
                BeginNewPackage(blockNumber);
            }

            _currentRoot = trieNode ?? _currentRoot;

            if (trieNode != null)
            {
                if (trieNode.Keccak == null)
                {
                    throw new InvalidOperationException(
                        $"Hash of the node {trieNode} should be known at the time of committing.");
                }

                Debug.Assert(CurrentPackage != null, "Current package is null when enqueing a trie node.");

                if (_inMemNodes.ContainsKey(trieNode.Keccak))
                {
                    // TODO: check if this solves the checklist problem
                    // _inMemNodes[trieNode.Keccak].Refs += trieNode.Refs;
                }
                else
                {
                    long previousPackageMemory = CurrentPackage.MemorySize;
                    CurrentPackage.Enqueue(trieNode);
                    _inMemNodes[trieNode.Keccak] = trieNode;
                    AddToMemory(CurrentPackage.MemorySize - previousPackageMemory);
                }
            }
        }

        public void Uncommit()
        {
            if (_queue.Count == 0)
            {
                throw new InvalidOperationException("Trying to uncommit a block when queue is empty");
            }

            _queue.RemoveLast();
        }

        public void Flush()
        {
            _checkList.Clear();
            CurrentPackage?.Seal();
            while (TryDispatchOne()) { }
        }

        public byte[] this[byte[] key] => _keyValueStore[key];

        public TrieNode FindCached(Keccak key)
        {
            return _inMemNodes.TryGetValue(key, out TrieNode trieNode) ? trieNode : null;
        }

        public long MemorySize { get; private set; }

        #region Private

        private const int LinkedListNodeMemorySize = 48;

        private readonly IKeyValueStore _keyValueStore;

        private readonly ILogger _logger;

        private readonly long _memoryLimit;

        private LinkedList<BlockCommitPackage> _queue = new LinkedList<BlockCommitPackage>();
        
        private Dictionary<Keccak, TrieNode> _inMemNodes = new Dictionary<Keccak, TrieNode>();

        private BlockCommitPackage? CurrentPackage => _queue.Last?.Value;

        private bool IsCurrentPackageSealed => CurrentPackage == null || CurrentPackage.IsSealed;

        private Stack<TrieNode> _rootStack = new Stack<TrieNode>();

        private TrieNode _currentRoot;
        
        private void BeginNewPackage(long blockNumber)
        {
            if (_logger.IsTrace)
                _logger.Trace($"Beginning new {nameof(BlockCommitPackage)} - {blockNumber} | memory {MemorySize}");
            
            Debug.Assert(CurrentPackage == null || CurrentPackage.BlockNumber == blockNumber - 1,
                "Newly begun block is not a successor of the last one");

            CurrentPackage?.Seal();
            if (_currentRoot != null)
            {
                _rootStack.Push(_currentRoot);
                _currentRoot.ReferenceRecursively();
            }
            
            Debug.Assert(IsCurrentPackageSealed, "Not sealed when beginning new block");

            BlockCommitPackage newPackage = new BlockCommitPackage(blockNumber);
            _queue.AddLast(newPackage);

            long newMemory = newPackage.MemorySize + LinkedListNodeMemorySize;
            AddToMemory(newMemory);

            Debug.Assert(CurrentPackage == newPackage,
                "Current package is not equal the new package just after adding");
        }

        private void AddToMemory(long newMemory)
        {
            while (MemorySize + newMemory > _memoryLimit)
            {
                bool success = TryDispatchOne();
                if (!success)
                {
                    break;
                }
            }

            if (MemorySize + newMemory > _memoryLimit)
            {
                if(_logger.IsTrace)
                    _logger.Trace($"Not able to dispatch to decrease memory usage below the limit of {_memoryLimit}.");
            }

            MemorySize += newMemory;
        }

        private bool TryDispatchOne()
        {
            BlockCommitPackage package = _queue.First?.Value;
            bool canDispatch = package?.IsSealed ?? false;
            if (canDispatch)
            {
                Dispatch(package);
                _queue.RemoveFirst();
            }

            return canDispatch;
        }
        
        private List<TrieNode> _checkList = new List<TrieNode>();
        
        private void Dispatch(BlockCommitPackage commitPackage)
        {
            if (_logger.IsDebug)
                _logger.Debug(
                    $"Start dispatching {nameof(BlockCommitPackage)} - {commitPackage.BlockNumber} | memory {MemorySize}");

            Debug.Assert(commitPackage != null && commitPackage.IsSealed,
                $"Invalid {nameof(commitPackage)} - {commitPackage} received for dispatch.");

            long memoryToDrop = commitPackage.MemorySize + LinkedListNodeMemorySize;

            TrieNode root = null;
            while (commitPackage.TryDequeue(out TrieNode currentNode))
            {
                root = currentNode; // root will be the last one
                _checkList.Add(currentNode);
                Debug.Assert(currentNode.Keccak != null, "Committed node has a NULL key");
                Debug.Assert(currentNode.FullRlp != null, $"Committed nad has a NULL {nameof(currentNode.FullRlp)}");

                // if ref count is zero then just discard
                if (currentNode.Refs == 0)
                {
                    if (_queue.Count > 0)
                    {
                        throw new Exception("Temporarily - this should not happen with the current code");
                    }

                    if (_logger.IsTrace)
                        _logger.Trace($"Dropping a {nameof(TrieNode)} {currentNode} with a zero ref count.");
                    _inMemNodes.Remove(currentNode.Keccak);
                    continue;
                }
                
                // start a batch here? (need to resolve responsibility between here and StateDb)
                if (_logger.IsTrace)
                    _logger.Trace($"Saving a {nameof(TrieNode)} {currentNode}.");
                _keyValueStore[currentNode.Keccak.Bytes] = currentNode.FullRlp;
            }
            
            // TODO: so here we HAD a big problem of same nodes represented multiple times as .NET objects and having mismatched refs
            if (root != null && root.Refs != 0)
            {
                root.DereferenceRecursively();
            }

            MemorySize -= memoryToDrop;
            if (_logger.IsDebug)
                _logger.Debug(
                    $"End dispatching {nameof(BlockCommitPackage)} - {commitPackage.BlockNumber} | memory {MemorySize}");
        }

        #endregion
    }
}