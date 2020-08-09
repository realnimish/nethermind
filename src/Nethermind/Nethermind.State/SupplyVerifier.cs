//  Copyright (c) 2018 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
// 

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Dirichlet.Numerics;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Trie;

namespace Nethermind.State
{
    public class SupplyVerifier : ITreeVisitor
    {
        private readonly ILogger _logger;
        private UInt256 _balance = UInt256.Zero;
        private Keccak _ignoreThisOne;
        private int _accountsVisited;
        private int _nodesVisited;
        private int _missing;

        public SupplyVerifier(ILogger logger)
        {
            _logger = logger;
        }

        public bool ShouldVisit(Keccak nextNode)
        {
            if (_ignoreThisOne != null)
            {
                if (_ignoreThisOne == nextNode)
                {
                    _ignoreThisOne = null;
                    return false;
                }
            }

            return true;
        }

        public void VisitTree(Keccak rootHash, TrieVisitContext trieVisitContext)
        {
        }

        public void VisitMissingNode(Keccak nodeHash, TrieVisitContext trieVisitContext)
        {
            _logger.Warn($"Missing node {nodeHash}");
        }

        public void VisitBranch(TrieNode node, TrieVisitContext trieVisitContext)
        {
            _logger.Info($"Balance after visiting {_accountsVisited} accounts and {_nodesVisited} nodes: {_balance}");
            _nodesVisited++;
        }

        public void VisitExtension(TrieNode node, TrieVisitContext trieVisitContext)
        {
            _nodesVisited++;
            if (trieVisitContext.IsStorage)
            {
                 _ignoreThisOne = node.GetChildHash(0);
            }
        }

        public void VisitLeaf(TrieNode node, TrieVisitContext trieVisitContext, byte[] value = null)
        {
            _nodesVisited++;
            
            if (trieVisitContext.IsStorage)
            {
                return;
            }
            
            AccountDecoder accountDecoder = new AccountDecoder();
            Account account = accountDecoder.Decode(node.Value.AsRlpStream());
            _balance += account.Balance;
            _accountsVisited++;
                
            _logger.Info($"Balance after visiting {_accountsVisited} accounts and {_nodesVisited} nodes: {_balance}");
        }

        public void VisitCode(Keccak codeHash, TrieVisitContext trieVisitContext)
        {
            _nodesVisited++;
        }
    }
}