using Xunit;

// Every golden scene shares one GL device, and a second concurrent Game fails intermittently on
// Linux (see SharedGame in GraphicsDeviceFixture.cs). The suite is small; run it serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
