using System;
using NUnit.Framework;
using omnishim;

namespace omnishim_test_core
{
    [TestFixture()]
    public class SetupTests
    {
        [SetUp()]
        public void Setup()
        {
            typeof(OmniShim).GetField("instance", System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).SetValue(null, null);
        }

        [Test()]
        public void Register_WithoutPrestart_ThrowsException()
        {
            Assert.Throws(typeof(Exception), () => OmniShim.RegisterForInterface<SuccessfulRegisterClass>());
        }
    }
}
