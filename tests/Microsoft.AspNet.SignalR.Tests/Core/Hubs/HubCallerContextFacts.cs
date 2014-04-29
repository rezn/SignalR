using Microsoft.AspNet.SignalR.Hubs;
using Moq;
using Xunit;

namespace Microsoft.AspNet.SignalR.Tests.Core.Hubs
{
    public class HubCallerContextFacts
    {
        [Fact]
        public void HubCallerContextCanBeMocked()
        {
            var hubCallerContext = new Mock<HubCallerContext>();
            hubCallerContext.Setup<string>(h => h.ConnectionId).Returns("test");
            Assert.Equal("test", hubCallerContext.Object.ConnectionId);
        }
    }
}
