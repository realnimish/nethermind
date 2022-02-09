﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Nethermind.Abi;
using Nethermind.AccountAbstraction.Contracts;
using Nethermind.AccountAbstraction.Data;
using Nethermind.AccountAbstraction.Executor;
using Nethermind.AccountAbstraction.Network;
using Nethermind.AccountAbstraction.Source;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Blockchain.Contracts.Json;
using Nethermind.Blockchain.Producers;
using Nethermind.Consensus;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;
using Nethermind.Logging;
using Nethermind.Network;
using Nethermind.Network.P2P;
using Nethermind.Stats;
using Nethermind.Stats.Model;
using Nethermind.Mev;
using Nethermind.AccountAbstraction.Bundler;
using Nethermind.Blockchain.Filters;
using Nethermind.Blockchain.Filters.Topics;
using Nethermind.Blockchain.Find;

namespace Nethermind.AccountAbstraction
{
    public class AccountAbstractionPlugin : IConsensusWrapperPlugin
    {
        private IAccountAbstractionConfig _accountAbstractionConfig = null!;
        private Address _create2FactoryAddress = null!;
        private AbiDefinition _entryPointContractAbi = null!;
        private ILogger _logger = null!;

        private INethermindApi _nethermindApi = null!;
        private IList<Address> _entryPointContractAddresses = new List<Address>();
        private IDictionary<Address, UserOperationPool> _userOperationPools = new Dictionary<Address, UserOperationPool>(); // EntryPoint Address -> Pool
        private IDictionary<Address, UserOperationSimulator> _userOperationSimulators = new Dictionary<Address, UserOperationSimulator>();
        private IDictionary<Address, UserOperationTxBuilder> _userOperationTxBuilders = new Dictionary<Address, UserOperationTxBuilder>();
        private UserOperationTxSource? _userOperationTxSource;
        private IBundler? _bundler;

        private MevPlugin MevPlugin => _nethermindApi
            .GetConsensusWrapperPlugins()
            .OfType<MevPlugin>()
            .Single();

        private UserOperationTxBuilder UserOperationTxBuilder(Address entryPoint)
        {
            if (_userOperationTxBuilders.TryGetValue(entryPoint, out UserOperationTxBuilder? userOperationTxBuilder))
            {

                return userOperationTxBuilder;
            }

            var (getFromApi, _) = _nethermindApi!.ForProducer;
            
            _userOperationTxBuilders[entryPoint] = new UserOperationTxBuilder(
                _entryPointContractAbi,
                getFromApi.EngineSigner!,
                entryPoint,
                getFromApi.SpecProvider!,
                getFromApi.StateProvider!);

            return _userOperationTxBuilders[entryPoint];
        }
        
        private UserOperationPool UserOperationPool(Address entryPoint)
        {
            if (_userOperationPools.TryGetValue(entryPoint, out UserOperationPool? userOperationPool))
            {
<<<<<<< HEAD

=======
>>>>>>> Support added for multiple entry points-1
                return userOperationPool;
            }

            var (getFromApi, _) = _nethermindApi!.ForProducer;

            UserOperationSortedPool userOperationSortedPool = new UserOperationSortedPool(
                _accountAbstractionConfig.UserOperationPoolSize,
                CompareUserOperationsByDecreasingGasPrice.Default,
                getFromApi.LogManager);

            _userOperationPools[entryPoint] = new UserOperationPool(
                accountAbstractionConfig,
                _nethermindApi.BlockTree!,
                entryPoint,
                _logger,
                new PaymasterThrottler(BundleMiningEnabled),
                _nethermindApi.LogFinder!,
                _nethermindApi.EngineSigner!,
                _nethermindApi.StateProvider!,
                _nethermindApi.Timestamper,
                UserOperationSimulator(entryPoint),
                userOperationSortedPool);

            return _userOperationPools[entryPoint];
            //     if (_userOperationPool is null)
            //     {
            //         var (getFromApi, _) = _nethermindApi!.ForProducer;
            //         UserOperationSortedPool userOperationSortedPool = new(
            //             _accountAbstractionConfig.UserOperationPoolSize,
            //             CompareUserOperationsByDecreasingGasPrice.Default,
            //             getFromApi.LogManager);

            //         _userOperationPool = new UserOperationPool(
            //             _accountAbstractionConfig,
            //             _nethermindApi.BlockTree!,
            //             _entryPointContractAddress,
            //             _logger,
            //             new PaymasterThrottler(BundleMiningEnabled),
            //             _nethermindApi.LogFinder!,
            //             _nethermindApi.EngineSigner!,
            //             _nethermindApi.StateProvider!,
            //             _nethermindApi.Timestamper,
            //             UserOperationSimulator,
            //             userOperationSortedPool);
            //     }

            //     return _userOperationPool;
            // }
        }

        private UserOperationSimulator UserOperationSimulator (Address entryPoint)
        {
            if(_userOperationSimulators[entryPoint].TryGetValue(entryPoint, out UserOperationSimulator userOperationSimulator))
            {
                return userOperationSimulator;
            }

            var (getFromApi, _) = _nethermindApi!.ForProducer;

            _userOperationSimulators[entryPoint] = new userOperationSimulator(
                UserOperationTxBuilder(entryPoint),
                getFromApi.StateProvider!,
                getFromApi.StateReader!,
                _entryPointContractAbi,
                _create2FactoryAddress,
                entryPoint,
                getFromApi.SpecProvider!,
                getFromApi.BlockTree!,
                getFromApi.DbProvider!,
                getFromApi.ReadOnlyTrieStore!,
                getFromApi.Timestamper!,
                getFromApi.LogManager);

            return _userOperationSimulators[entryPoint];
            // get
            // {
            //     if (_userOperationSimulator is null)
            //     {
            //         var (getFromApi, _) = _nethermindApi!.ForProducer;

            //         _userOperationSimulator = new UserOperationSimulator(
            //             UserOperationTxBuilder,
            //             getFromApi.StateProvider!,
            //             getFromApi.StateReader!,
            //             _entryPointContractAbi,
            //             _create2FactoryAddress,
            //             _entryPointContractAddress,
            //             getFromApi.SpecProvider!,
            //             getFromApi.BlockTree!,
            //             getFromApi.DbProvider!,
            //             getFromApi.ReadOnlyTrieStore!,
            //             getFromApi.Timestamper!,
            //             getFromApi.LogManager);
            //     }

            //     return _userOperationSimulator;
            // }
        }

        private UserOperationTxSource UserOperationTxSource
        {
            get
            {
                if (_userOperationTxSource is null)
                {
                    var (getFromApi, _) = _nethermindApi!.ForProducer;

                    IDictionary<Address, UserOperationPool> _Pools = new Dictionary<Address, UserOperationPool>(); // EntryPoint Address -> Pool
                    IDictionary<Address, UserOperationSimulator> _Simulators = new Dictionary<Address, UserOperationSimulator>();
                    IDictionary<Address, UserOperationTxBuilder> _TxBuilders = new Dictionary<Address, UserOperationTxBuilder>();

                    foreach(Address entryPoint in _entryPointContractAddresses)
                    {
                        _Pools[entryPoint] = UserOperationPool(entryPoint);
                        _Simulators[entryPoint] = UserOperationSimulator(entryPoint);
                        _TxBuilders[entryPoint] = UserOperationTxBuilder(entryPoint);
                    }

                    _userOperationTxSource = new UserOperationTxSource
                    (
                        _TxBuilders,
                        _Pools,
                        _Simulators,
                        _nethermindApi.SpecProvider!,
                        _logger
                    );
                }

                return _userOperationTxSource;
            }
        }

        public string Name => "Account Abstraction";

        public string Description => "Implements account abstraction via alternative mempool (ERC-4337)";

        public string Author => "Nethermind";

        public Task Init(INethermindApi nethermindApi)
        {
            _nethermindApi = nethermindApi ?? throw new ArgumentNullException(nameof(nethermindApi));
            _accountAbstractionConfig = _nethermindApi.Config<IAccountAbstractionConfig>();
            _logger = _nethermindApi.LogManager.GetClassLogger();

            if (_accountAbstractionConfig.Enabled)
            {

                // bool parsed = Address.TryParse(
                //     _accountAbstractionConfig.EntryPointContractAddress,
                //     out Address? entryPointContractAddress);
                // if (!parsed)
                // {
                //     if (_logger.IsError) _logger.Error("Account Abstraction Plugin: EntryPoint contract address could not be parsed");
                // }
                // else
                // {
                //     if (_logger.IsInfo) _logger.Info($"Parsed EntryPoint Address: {entryPointContractAddress}");
                //     _entryPointContractAddress = entryPointContractAddress!;
                // }

                IList<string> _entryPointContractAddressesString = _accountAbstractionConfig.GetEntryPointAddresses();
                foreach (string _addressString in _entryPointContractAddressesString){
                    bool parsed = Address.TryParse(
                        _addressString,
                        out Address? entryPointContractAddress);
                    if (!parsed)
                    {
                        if (_logger.IsError) _logger.Error("Account Abstraction Plugin: EntryPoint contract address could not be parsed");
                    }
                    else
                    {
                        if (_logger.IsInfo) _logger.Info($"Parsed EntryPoint Address: {entryPointContractAddress}");
                        _entryPointContractAddresses.Add(entryPointContractAddress!);
                    }
                }

                bool parsedCreate2Factory = Address.TryParse(
                    _accountAbstractionConfig.Create2FactoryAddress,
                    out Address? create2FactoryAddress);
                if (!parsedCreate2Factory)
                {
                    if (_logger.IsError) _logger.Error("Account Abstraction Plugin: Create2Factory contract address could not be parsed");
                }
                else
                {
                    if (_logger.IsInfo) _logger.Info($"Parsed Create2Factory Address: {create2FactoryAddress}");
                    _create2FactoryAddress = create2FactoryAddress!;
                }

                _entryPointContractAbi = LoadEntryPointContract();
            }

            if (Enabled)
            {
                if (_logger.IsInfo) _logger.Info("  Account Abstraction Plugin: User Operation Mining Enabled");
            }
            else
            {
                if (_logger.IsInfo) _logger.Info("  Account Abstraction Plugin: User Operation Mining Disabled");
            }

            return Task.CompletedTask;
        }

        public Task InitNetworkProtocol()
        {
            if (_accountAbstractionConfig.Enabled)
            {
                if (_nethermindApi is null) throw new ArgumentNullException(nameof(_nethermindApi));
                if (UserOperationPool is null) throw new ArgumentNullException(nameof(UserOperationPool));

                IProtocolsManager protocolsManager = _nethermindApi.ProtocolsManager ??
                                                     throw new ArgumentNullException(
                                                         nameof(_nethermindApi.ProtocolsManager));
                IMessageSerializationService serializer = _nethermindApi.MessageSerializationService ??
                                                          throw new ArgumentNullException(
                                                              nameof(_nethermindApi.MessageSerializationService));
                INodeStatsManager stats = _nethermindApi.NodeStatsManager ??
                                          throw new ArgumentNullException(nameof(_nethermindApi.NodeStatsManager));
                ILogManager logManager = _nethermindApi.LogManager ??
                                         throw new ArgumentNullException(nameof(_nethermindApi.LogManager));

                serializer.Register(new UserOperationsMessageSerializer());
                protocolsManager.AddProtocol(Protocol.AA,
                    session => new AaProtocolHandler(session, serializer, stats, UserOperationPool, logManager));
                protocolsManager.AddSupportedCapability(new Capability(Protocol.AA, 0));

                if (_logger.IsInfo) _logger.Info("Initialized Account Abstraction network protocol");
            }
            else
            {
                if (_logger.IsInfo) _logger.Info("Skipping Account Abstraction network protocol");
            }

            return Task.CompletedTask;
        }

        public Task InitRpcModules()
        {
            if (_accountAbstractionConfig.Enabled)
            {
                (IApiWithNetwork getFromApi, _) = _nethermindApi!.ForRpc;

                IJsonRpcConfig rpcConfig = getFromApi.Config<IJsonRpcConfig>();
                rpcConfig.EnableModules(ModuleType.AccountAbstraction);

                // AccountAbstractionModuleFactory accountAbstractionModuleFactory = new(UserOperationPool, new[] {_entryPointContractAddress});
                AccountAbstractionModuleFactory accountAbstractionModuleFactory = new(UserOperationPool, _entryPointContractAddresses.ToArray());

                getFromApi.RpcModuleProvider!.RegisterBoundedByCpuCount(accountAbstractionModuleFactory, rpcConfig.Timeout);

                if (BundleMiningEnabled && MevPluginEnabled)
                {
                    if (_logger!.IsInfo) _logger.Info("Both AA and MEV Plugins enabled, sending user operations to mev bundle pool instead");
                    _bundler = new MevBundler(
                        new OnNewBlockBundleTrigger(_nethermindApi.BlockTree!, _logger),
                        UserOperationTxSource, MevPlugin.BundlePool,
                        _logger
                    );
                }


                if (_logger!.IsInfo) _logger.Info("Account Abstraction RPC plugin enabled");
            }
            else
            {
                if (_logger!.IsWarn) _logger.Info("Skipping Account Abstraction RPC plugin");
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public Task<IBlockProducer> InitBlockProducer(IConsensusPlugin consensusPlugin)
        {
            if (!Enabled) throw new InvalidOperationException("Account Abstraction plugin is disabled");

            UInt256 minerBalance = _nethermindApi.StateProvider!.GetBalance(_nethermindApi.EngineSigner!.Address);
            if (minerBalance < 1.Ether())
                if (_logger.IsWarn) _logger.Warn(
                    $"Account Abstraction Plugin: Miner ({_nethermindApi.EngineSigner!.Address}) Ether balance low - {minerBalance / 1.Ether()} Ether < 1 Ether. Increasing balance is recommended");
            else
            {
                if (_logger.IsInfo)
                    _logger.Info(
                        $"Account Abstraction Plugin: Miner ({_nethermindApi.EngineSigner!.Address}) Ether balance adequate - {minerBalance / 1.Ether()} Ether");
            }

            IManualBlockProductionTrigger trigger = new BuildBlocksWhenRequested();

            return consensusPlugin.InitBlockProducer(trigger, UserOperationTxSource);
        }

        public bool MevPluginEnabled => _nethermindApi.Config<IMevConfig>().Enabled;
        public bool BundleMiningEnabled => _accountAbstractionConfig.Enabled && (_nethermindApi.Config<IInitConfig>().IsMining || _nethermindApi.Config<IMiningConfig>().Enabled);
        public bool Enabled => BundleMiningEnabled && !MevPluginEnabled; // IConsensusWrapperPlugin.Enabled

        private AbiDefinition LoadEntryPointContract()
        {
            AbiDefinitionParser parser = new();
            parser.RegisterAbiTypeFactory(new AbiTuple<UserOperationAbi>());
            string json = parser.LoadContract(typeof(EntryPoint));
            return parser.Parse(json);
        }
    }
}
