//
// Copyright (c) ZeroC, Inc. All rights reserved.
//

using Test;
using Ice;
using System.Reflection;
using System.Threading.Tasks;

[assembly: AssemblyTitle("IceTest")]
[assembly: AssemblyDescription("Ice test")]
[assembly: AssemblyCompany("ZeroC, Inc.")]

public class Server : TestHelper
{
    internal class Locator : ILocator
    {
        public Task<IObjectPrx>
        FindAdapterByIdAsync(string adapter, Current current)
        {
            _controller.checkCallPause(current);
            return Task.FromResult(current.Adapter.CreateDirectProxy("dummy"));
        }

        public Task<IObjectPrx>
        FindObjectByIdAsync(Identity id, Current current)
        {
            _controller.checkCallPause(current);
            return Task.FromResult(current.Adapter.CreateDirectProxy(id));
        }

        public ILocatorRegistryPrx GetRegistry(Current current) => null;

        internal Locator(BackgroundController controller) => _controller = controller;

        private BackgroundController _controller;
    }

    internal class RouterI : IRouter
    {
        public IRouter.GetClientProxyReturnValue GetClientProxy(Current current)
        {
            _controller.checkCallPause(current);
            return new IRouter.GetClientProxyReturnValue(null, true);
        }

        public IObjectPrx GetServerProxy(Current current)
        {
            _controller.checkCallPause(current);
            return null;
        }

        public IObjectPrx[] AddProxies(IObjectPrx[] proxies, Current current) => new IObjectPrx[0];

        internal RouterI(BackgroundController controller) => _controller = controller;

        private BackgroundController _controller;
    }

    public override void run(string[] args)
    {
        var properties = createTestProperties(ref args);
        //
        // This test kills connections, so we don't want warnings.
        //
        properties["Ice.Warn.Connections"] = "0";

        properties["Ice.MessageSizeMax"] = "50000";

        // This test relies on filling the TCP send/recv buffer, so
        // we rely on a fixed value for these buffers.
        properties["Ice.TCP.RcvSize"] = "50000";

        //
        // Setup the test transport plug-in.
        //
        string? protocol;
        if (!properties.TryGetValue("Ice.Default.Protocol", out protocol))
        {
            protocol = "tcp";
        }
        properties["Ice.Default.Protocol"] = $"test-{protocol}";

        using var communicator = initialize(properties);
        var plugin = new Plugin(communicator);
        plugin.initialize();
        communicator.AddPlugin("Test", plugin);

        //
        // When running as a MIDlet the properties for the server may be
        // overridden by configuration. If it isn't then we assume
        // defaults.
        //
        if (communicator.GetProperty("TestAdapter.Endpoints") == null)
        {
            communicator.SetProperty("TestAdapter.Endpoints", getTestEndpoint(0));
        }

        if (communicator.GetProperty("ControllerAdapter.Endpoints") == null)
        {
            communicator.SetProperty("ControllerAdapter.Endpoints", getTestEndpoint(1, "tcp"));
            communicator.SetProperty("ControllerAdapter.ThreadPool.Size", "1");
        }

        ObjectAdapter adapter = communicator.createObjectAdapter("TestAdapter");
        ObjectAdapter adapter2 = communicator.createObjectAdapter("ControllerAdapter");

        var backgroundController = new BackgroundController(adapter);

        var backgroundI = new Background(backgroundController);

        adapter.Add(backgroundI, "background");

        Locator locatorI = new Locator(backgroundController);
        adapter.Add(locatorI, "locator");

        RouterI routerI = new RouterI(backgroundController);
        adapter.Add(routerI, "router");
        adapter.Activate();

        adapter2.Add(backgroundController, "backgroundController");
        adapter2.Activate();

        communicator.waitForShutdown();
    }

    public static int Main(string[] args) => TestDriver.runTest<Server>(args);
}
