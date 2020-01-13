//
// Copyright (c) ZeroC, Inc. All rights reserved.
//

using Test;

using Ice.ami.Test;
using Ice.ami.Test.Outer.Inner;

namespace Ice.ami
{
    public class Collocated : TestHelper
    {
        public override void run(string[] args)
        {
            var properties = createTestProperties(ref args);

            properties["Ice.Warn.AMICallback"] = "0";
            //
            // Limit the send buffer size, this test relies on the socket
            // send() blocking after sending a given amount of data.
            //
            properties["Ice.TCP.SndSize"] = "50000";
            //
            // We use a client thread pool with more than one thread to test
            // that task inlining works.
            //
            properties["Ice.ThreadPool.Client.Size"] = "5";
            using var communicator = initialize(properties);

            communicator.SetProperty("TestAdapter.Endpoints", getTestEndpoint(0));
            communicator.SetProperty("ControllerAdapter.Endpoints", getTestEndpoint(1));
            communicator.SetProperty("ControllerAdapter.ThreadPool.Size", "1");

            var adapter = communicator.createObjectAdapter("TestAdapter");
            var adapter2 = communicator.createObjectAdapter("ControllerAdapter");

            adapter.Add(new TestIntf(), "test");
            adapter.Add(new TestIntf2(), "test2");
            //adapter.activate(); // Collocated test doesn't need to activate the OA
            adapter2.Add(new TestControllerIntf(adapter), "testController");
            //adapter2.activate(); // Collocated test doesn't need to activate the OA

            AllTests.allTests(this, true);
        }

        public static int Main(string[] args) => TestDriver.runTest<Collocated>(args);
    }
}
