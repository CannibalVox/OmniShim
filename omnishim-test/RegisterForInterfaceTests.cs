using NUnit.Framework;
using System;
namespace omnishimtest
{
    public class FailedRegisterClass
    {

    }

    public interface ISimpleInterface
    {
        void DoThing(int wow);
    }

    [SetUpFixture()]
    [TestFixture()]
    public class Test
    {
        [OneTimeSetUp()]
        public void SetUp()
        {
            OmniShim
        }
        [Test()]
        public void TestCase()
        {
            
        }
    }
}
