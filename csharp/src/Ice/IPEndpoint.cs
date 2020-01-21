//
// Copyright (c) ZeroC, Inc. All rights reserved.
//

namespace IceInternal
{

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;

    public abstract class IPEndpoint : Endpoint
    {
        public IPEndpoint(ProtocolInstance instance, string host, int port, EndPoint? sourceAddr, string connectionId)
        {
            instance_ = instance;
            host_ = host;
            port_ = port;
            sourceAddr_ = sourceAddr;
            connectionId_ = connectionId;
            _hashInitialized = false;
        }

        public IPEndpoint(ProtocolInstance instance)
        {
            instance_ = instance;
            host_ = null;
            port_ = 0;
            sourceAddr_ = null;
            connectionId_ = "";
            _hashInitialized = false;
        }

        public IPEndpoint(ProtocolInstance instance, Ice.InputStream s)
        {
            instance_ = instance;
            host_ = s.ReadString();
            port_ = s.ReadInt();
            sourceAddr_ = null;
            connectionId_ = "";
            _hashInitialized = false;
        }

        private sealed class Info : Ice.IPEndpointInfo
        {
            public Info(IPEndpoint e) => _endpoint = e;

            public override short type() => _endpoint.type();

            public override bool datagram() => _endpoint.datagram();

            public override bool secure() => _endpoint.secure();

            private IPEndpoint _endpoint;
        }

        public override Ice.EndpointInfo getInfo()
        {
            Info info = new Info(this);
            fillEndpointInfo(info);
            return info;
        }

        public override short type() => instance_.Type;

        public override string protocol() => instance_.Protocol;

        public override bool secure() => instance_.Secure;

        public override string connectionId() => connectionId_;

        public override Endpoint connectionId(string connectionId)
        {
            if (connectionId.Equals(connectionId_))
            {
                return this;
            }
            else
            {
                return CreateEndpoint(host_, port_, connectionId);
            }
        }

        public override void ConnectorsAsync(Ice.EndpointSelectionType selType, IEndpointConnectors callback) =>
            instance_.Resolve(host_!, port_, selType, this, callback);

        public override List<Endpoint> expandIfWildcard()
        {
            List<Endpoint> endps = new List<Endpoint>();
            List<string> hosts = Network.getHostsForEndpointExpand(host_!, instance_.ProtocolSupport, false);
            if (hosts == null || hosts.Count == 0)
            {
                endps.Add(this);
            }
            else
            {
                foreach (string h in hosts)
                {
                    endps.Add(CreateEndpoint(h, port_, connectionId_));
                }
            }
            return endps;
        }

        public override List<Endpoint> expandHost(out Endpoint? publish)
        {
            //
            // If this endpoint has an empty host (wildcard address), don't expand, just return
            // this endpoint.
            //
            var endpoints = new List<Endpoint>();
            if (host_.Length == 0)
            {
                publish = null;
                endpoints.Add(this);
                return endpoints;
            }

            //
            // If using a fixed port, this endpoint can be used as the published endpoint to
            // access the returned endpoints. Otherwise, we'll publish each individual expanded
            // endpoint.
            //
            publish = port_ > 0 ? this : null;

            List<EndPoint> addresses = Network.getAddresses(host_,
                                                            port_,
                                                            instance_.ProtocolSupport,
                                                            Ice.EndpointSelectionType.Ordered,
                                                            instance_.PreferIPv6,
                                                            true);

            if (addresses.Count == 1)
            {
                endpoints.Add(this);
            }
            else
            {
                foreach (EndPoint addr in addresses)
                {
                    endpoints.Add(CreateEndpoint(Network.endpointAddressToString(addr),
                                                 Network.endpointPort(addr),
                                                 connectionId_));
                }
            }
            return endpoints;
        }

        public override bool equivalent(Endpoint endpoint)
        {
            if (!(endpoint is IPEndpoint))
            {
                return false;
            }
            IPEndpoint ipEndpointI = (IPEndpoint)endpoint;
            return ipEndpointI.type() == type() &&
                Equals(ipEndpointI.host_, host_) &&
                ipEndpointI.port_ == port_ &&
                Equals(ipEndpointI.sourceAddr_, sourceAddr_);
        }

        public virtual List<IConnector> connectors(List<EndPoint> addresses, INetworkProxy? proxy)
        {
            List<IConnector> connectors = new List<IConnector>();
            foreach (EndPoint p in addresses)
            {
                connectors.Add(CreateConnector(p, proxy));
            }
            return connectors;
        }

        public override string options()
        {
            //
            // WARNING: Certain features, such as proxy validation in Glacier2,
            // depend on the format of proxy strings. Changes to toString() and
            // methods called to generate parts of the reference string could break
            // these features. Please review for all features that depend on the
            // format of proxyToString() before changing this and related code.
            //
            string s = "";

            if (host_ != null && host_.Length > 0)
            {
                s += " -h ";
                bool addQuote = host_.IndexOf(':') != -1;
                if (addQuote)
                {
                    s += "\"";
                }
                s += host_;
                if (addQuote)
                {
                    s += "\"";
                }
            }

            s += " -p " + port_;

            if (sourceAddr_ != null)
            {
                string sourceAddr = Network.endpointAddressToString(sourceAddr_);
                bool addQuote = sourceAddr.IndexOf(':') != -1;
                s += " --sourceAddress ";
                if (addQuote)
                {
                    s += "\"";
                }
                s += sourceAddr;
                if (addQuote)
                {
                    s += "\"";
                }
            }

            return s;
        }

        public override int GetHashCode()
        {
            if (!_hashInitialized)
            {
                _hashValue = 5381;
                HashUtil.hashAdd(ref _hashValue, type());
                hashInit(ref _hashValue);
                _hashInitialized = true;
            }
            return _hashValue;
        }

        public override int CompareTo(Endpoint obj)
        {
            if (!(obj is IPEndpoint))
            {
                return type() < obj.type() ? -1 : 1;
            }

            IPEndpoint p = (IPEndpoint)obj;
            if (this == p)
            {
                return 0;
            }

            int v = string.Compare(host_, p.host_, StringComparison.Ordinal);
            if (v != 0)
            {
                return v;
            }

            if (port_ < p.port_)
            {
                return -1;
            }
            else if (p.port_ < port_)
            {
                return 1;
            }

            int rc = string.Compare(Network.endpointAddressToString(sourceAddr_),
                                    Network.endpointAddressToString(p.sourceAddr_), StringComparison.Ordinal);
            if (rc != 0)
            {
                return rc;
            }

            return string.Compare(connectionId_, p.connectionId_, StringComparison.Ordinal);
        }

        public override void streamWriteImpl(Ice.OutputStream s)
        {
            s.WriteString(host_);
            s.WriteInt(port_);
        }

        public virtual void hashInit(ref int h)
        {
            HashUtil.hashAdd(ref h, host_);
            HashUtil.hashAdd(ref h, port_);
            if (sourceAddr_ != null)
            {
                HashUtil.hashAdd(ref h, sourceAddr_);
            }
            HashUtil.hashAdd(ref h, connectionId_);
        }

        public virtual void fillEndpointInfo(Ice.IPEndpointInfo info)
        {
            info.host = host_;
            info.port = port_;
            info.sourceAddress = Network.endpointAddressToString(sourceAddr_);
        }

        public virtual void initWithOptions(List<string> args, bool oaEndpoint)
        {
            base.initWithOptions(args);

            if (host_ == null || host_.Length == 0)
            {
                host_ = instance_.DefaultHost;
            }
            else if (host_.Equals("*"))
            {
                if (oaEndpoint)
                {
                    host_ = "";
                }
                else
                {
                    throw new FormatException($"`-h *' not valid for proxy endpoint `{this}'");
                }
            }

            if (host_ == null)
            {
                host_ = "";
            }

            if (sourceAddr_ != null)
            {
                if (oaEndpoint)
                {
                    throw new FormatException($"`--sourceAddress' not valid for object adapter endpoint `{this}'");
                }
            }
            else if (!oaEndpoint)
            {
                sourceAddr_ = instance_.DefaultSourceAddress;
            }
        }

        protected override bool checkOption(string option, string argument, string endpoint)
        {
            if (option.Equals("-h"))
            {
                if (argument == null)
                {
                    throw new FormatException($"no argument provided for -h option in endpoint {endpoint}");
                }
                host_ = argument;
            }
            else if (option.Equals("-p"))
            {
                if (argument == null)
                {
                    throw new FormatException($"no argument provided for -p option in endpoint {endpoint}");
                }

                try
                {
                    port_ = int.Parse(argument, CultureInfo.InvariantCulture);
                }
                catch (FormatException ex)
                {
                    throw new FormatException($"invalid port value `{argument}' in endpoint {endpoint}", ex);
                }

                if (port_ < 0 || port_ > 65535)
                {
                    throw new FormatException($"port value `{argument}' out of range in endpoint {endpoint}");
                }
            }
            else if (option.Equals("--sourceAddress"))
            {
                if (argument == null)
                {
                    throw new FormatException($"no argument provided for --sourceAddress option in endpoint {endpoint}");
                }
                sourceAddr_ = Network.getNumericAddress(argument);
                if (sourceAddr_ == null)
                {
                    throw new FormatException(
                        $"invalid IP address provided for --sourceAddress option in endpoint {endpoint}");
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        protected abstract IConnector CreateConnector(EndPoint addr, INetworkProxy? proxy);
        protected abstract IPEndpoint CreateEndpoint(string? host, int port, string connectionId);

        protected ProtocolInstance instance_;
        protected string? host_;
        protected int port_;
        protected EndPoint? sourceAddr_;
        protected string connectionId_;
        private bool _hashInitialized;
        private int _hashValue;
    }

}
