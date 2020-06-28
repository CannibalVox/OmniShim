using System;
using NUnit;

namespace OmniShim.Test {
    [TestFixture]
    public class HelloWorldTest {
        [Test]
        public void TestHelloWorld() {
            Assert.True(true);
            Assert.False(true);
        }
    }
}